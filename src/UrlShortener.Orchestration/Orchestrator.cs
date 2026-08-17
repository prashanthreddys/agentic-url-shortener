using System.Diagnostics;
using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Gates;
using UrlShortener.Orchestration.Governance;
using UrlShortener.Orchestration.Graph;
using UrlShortener.Orchestration.Observability;
using UrlShortener.Orchestration.Replanning;

namespace UrlShortener.Orchestration;

public sealed class OrchestratorOptions
{
    /// <summary>Upper bound on dynamic re-plans, so a flapping upstream cannot loop forever.</summary>
    public int MaxReplans { get; set; } = 3;
}

/// <summary>How a stage asked the pipeline to stop.</summary>
internal enum StopKind
{
    None,
    SafeStopNoRollback,
    SafeStopWithRollback
}

internal sealed record StageRunResult(StageStatus Status, StopKind Stop, string? Reason);

/// <summary>
/// Governed, stateful executor for an SDLC <see cref="StageGraph"/>. It schedules ready stages in
/// parallel waves with synchronization barriers, enforces entry/exit gates, policy guardrails and
/// human approvals, applies bounded retries with fallback, rolls back on failure and safe-stops,
/// re-plans when upstream outputs change, and records audit-grade observability plus reliability
/// metrics. Agents act autonomously only inside these boundaries (controlled autonomy).
/// </summary>
public sealed class Orchestrator
{
    private readonly IApprovalHandler _approvals;
    private readonly IReplanPolicy? _replanPolicy;
    private readonly OrchestratorOptions _options;

    public Orchestrator(IApprovalHandler approvals, IReplanPolicy? replanPolicy = null, OrchestratorOptions? options = null)
    {
        _approvals = approvals;
        _replanPolicy = replanPolicy;
        _options = options ?? new OrchestratorOptions();
    }

    public async Task<OrchestrationResult> RunAsync(StageGraph graph, CancellationToken ct = default)
    {
        var audit = new AuditLog(Guid.NewGuid().ToString("N")[..8]);
        var metrics = new ReliabilityMetrics { TotalStages = graph.Stages.Count };
        var blackboard = new Blackboard();
        var statuses = graph.Stages.Keys.ToDictionary(k => k, _ => StageStatus.Pending, StringComparer.OrdinalIgnoreCase);
        var completed = new List<string>();
        var firstFailureAt = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        var replanCount = 0;

        audit.Record("pipeline", AuditEventType.PipelineStarted, "Governance",
            $"Starting run of {graph.Stages.Count} stages. Topological order: {string.Join(" -> ", graph.TopologicalOrder())}.");

        var overall = Stopwatch.StartNew();
        PipelineStatus pipelineStatus = PipelineStatus.Succeeded;
        string? stopReason = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var ready = graph.Stages.Values
                .Where(s => statuses[s.Id] == StageStatus.Pending &&
                            s.DependsOn.All(d => statuses[d] == StageStatus.Succeeded))
                .ToList();

            if (ready.Count == 0)
            {
                if (TryReplan(graph, blackboard, statuses, audit, metrics, completed, ref replanCount))
                    continue;
                break;
            }

            // A wave: independent ready stages run in parallel, then synchronize at the barrier.
            foreach (var s in ready) statuses[s.Id] = StageStatus.Running;
            if (ready.Count > 1)
                audit.Record("pipeline", AuditEventType.StageReady, "Scheduling",
                    $"Parallel wave: {string.Join(", ", ready.Select(r => r.Id))} (synchronized barrier).");

            var results = await Task.WhenAll(ready.Select(node =>
                RunStageAsync(node, blackboard, audit, metrics, firstFailureAt, ct)));

            for (var i = 0; i < ready.Count; i++)
            {
                statuses[ready[i].Id] = results[i].Status;
                if (results[i].Status == StageStatus.Succeeded)
                    completed.Add(ready[i].Id);
            }

            var stopping = results.FirstOrDefault(r => r.Stop != StopKind.None);
            if (stopping is not null)
            {
                stopReason = stopping.Reason;
                pipelineStatus = PipelineStatus.SafeStopped;
                audit.Record("pipeline", AuditEventType.SafeStop, "Governance", $"Safe-stop: {stopReason}");
                if (stopping.Stop == StopKind.SafeStopWithRollback)
                    await RollbackAsync(graph, completed, statuses, blackboard, audit, metrics, ct);
                break;
            }

            if (TryReplan(graph, blackboard, statuses, audit, metrics, completed, ref replanCount))
                continue;
        }

        overall.Stop();
        metrics.EndToEndLatency = overall.Elapsed;

        if (pipelineStatus != PipelineStatus.SafeStopped)
        {
            pipelineStatus = statuses.Values.All(v => v == StageStatus.Succeeded)
                ? PipelineStatus.Succeeded
                : PipelineStatus.PartiallyCompleted;
        }

        audit.Record("pipeline", AuditEventType.PipelineCompleted, "Governance",
            $"Result={pipelineStatus}; success rate={metrics.SuccessRate:P0}; retries={metrics.Retries}; " +
            $"rollbacks={metrics.Rollbacks}; replans={metrics.Replans}; latency={metrics.EndToEndLatency.TotalMilliseconds:F0}ms.");

        return new OrchestrationResult
        {
            Status = pipelineStatus,
            StageStatuses = statuses,
            Metrics = metrics,
            Audit = audit,
            Blackboard = blackboard,
            StopReason = stopReason
        };
    }

    private async Task<StageRunResult> RunStageAsync(
        StageNode node, Blackboard blackboard, AuditLog audit, ReliabilityMetrics metrics,
        Dictionary<string, DateTimeOffset> firstFailureAt, CancellationToken ct)
    {
        metrics.MarkAttempted();
        var gateCtx = new GateContext(node.Id, blackboard, audit);
        audit.Record(node.Id, AuditEventType.StageReady, "Execution", node.Description);

        // 1) Entry gates: a failed entry gate is a governance HOLD (blocks downstream, no rollback).
        foreach (var gate in node.EntryGates)
        {
            var r = gate.Evaluate(gateCtx);
            audit.Record(node.Id, AuditEventType.GateEvaluated, "Gate",
                $"entry:{gate.Name} -> {(r.Passed ? "PASS" : "BLOCK")} ({r.Reason})");
            if (!r.Passed)
            {
                audit.RecordDecision(node.Id, "Hold stage", $"entry gate '{gate.Name}' blocked: {r.Reason}");
                return new StageRunResult(StageStatus.Blocked, StopKind.None, r.Reason);
            }
        }

        // 2) Policy guardrails: hard deny -> safe-stop with rollback; may escalate to approval.
        var guardrailWantsApproval = false;
        foreach (var g in node.Guardrails)
        {
            var gr = g.Evaluate(gateCtx);
            audit.Record(node.Id, AuditEventType.GuardrailEvaluated, "Guardrail",
                $"{g.Category}:{g.Name} -> {(gr.Allowed ? (gr.RequiresApproval ? "ALLOW+APPROVAL" : "ALLOW") : "DENY")} ({gr.Reason})");
            if (!gr.Allowed)
            {
                metrics.MarkGuardrailDenial();
                audit.RecordDecision(node.Id, "Safe-stop", $"{g.Category} guardrail '{g.Name}' denied: {gr.Reason}");
                return new StageRunResult(StageStatus.Blocked, StopKind.SafeStopWithRollback,
                    $"{g.Category} guardrail '{g.Name}' denied entry ({gr.Reason}).");
            }
            if (gr.RequiresApproval) guardrailWantsApproval = true;
        }

        // 3) Human approval for high-impact actions (controlled autonomy boundary).
        if (node.RequiresApproval || guardrailWantsApproval)
        {
            metrics.MarkApprovalRequested();
            audit.Record(node.Id, AuditEventType.ApprovalRequested, "Approval",
                $"Impact={node.Impact}; awaiting human decision.");
            var decision = _approvals.Request(new ApprovalRequest
            {
                StageId = node.Id,
                Reason = node.Description,
                Impact = node.Impact,
                Artifacts = blackboard.Artifacts.ToList()
            });
            audit.Record(node.Id, AuditEventType.ApprovalDecided, "Approval",
                $"{(decision.Approved ? "APPROVED" : "REJECTED")} by {decision.Approver}: {decision.Note}");
            if (!decision.Approved)
            {
                metrics.MarkApprovalRejected();
                audit.RecordDecision(node.Id, "Safe-stop", $"approval rejected by {decision.Approver}: {decision.Note}");
                // Nothing executed yet: preserve completed work, do not roll back.
                return new StageRunResult(StageStatus.Failed, StopKind.SafeStopNoRollback,
                    $"Approval rejected for '{node.Id}': {decision.Note}");
            }
        }

        // 4) Execute with bounded retries; each attempt must also clear exit gates.
        audit.Record(node.Id, AuditEventType.StageStarted, "Execution", "Agent executing.");
        var sw = Stopwatch.StartNew();
        StageOutcome? outcome = null;

        for (var attempt = 1; attempt <= node.Retry.MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                metrics.MarkRetry();
                var delay = node.Retry.DelayFor(attempt);
                audit.Record(node.Id, AuditEventType.StageAttempt, "Retry",
                    $"Retry {attempt}/{node.Retry.MaxAttempts} after backoff {delay.TotalMilliseconds:F0}ms.", attempt);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
            }

            var ctx = new StageContext(node.Id, blackboard, attempt, audit, ct);
            outcome = await SafeExecuteAsync(node.Agent, ctx);

            if (outcome.Success && PublishAndClearExitGates(node, outcome, blackboard, audit))
            {
                audit.Record(node.Id, AuditEventType.StageAttempt, "Execution",
                    $"Attempt {attempt} succeeded. {outcome.Note}", attempt);
                break;
            }

            outcome = outcome.Success ? StageOutcome.Fail("exit gate rejected output") : outcome;
            firstFailureAt.TryAdd(node.Id, DateTimeOffset.UtcNow);
            audit.Record(node.Id, AuditEventType.StageFailed, "Execution",
                $"Attempt {attempt} failed: {outcome.FailureReason}", attempt);
        }

        // 5) Fallback if still failing.
        if (outcome is { Success: false } && node.Fallback is not null)
        {
            audit.Record(node.Id, AuditEventType.FallbackInvoked, "Execution", "Primary exhausted; invoking fallback agent.");
            var ctx = new StageContext(node.Id, blackboard, node.Retry.MaxAttempts + 1, audit, ct);
            var fb = await SafeExecuteAsync(node.Fallback, ctx);
            if (fb.Success && PublishAndClearExitGates(node, fb, blackboard, audit))
                outcome = fb;
        }

        sw.Stop();
        metrics.RecordStageLatency(node.Id, sw.Elapsed);

        if (outcome is not { Success: true })
        {
            metrics.MarkFailed();
            audit.RecordDecision(node.Id, "Rollback + safe-stop", $"stage failed terminally: {outcome?.FailureReason}");
            return new StageRunResult(StageStatus.Failed, StopKind.SafeStopWithRollback,
                $"Stage '{node.Id}' failed: {outcome?.FailureReason}");
        }

        metrics.MarkSucceeded();
        if (firstFailureAt.TryGetValue(node.Id, out var failedAt))
            metrics.RecordRecovery(DateTimeOffset.UtcNow - failedAt);
        audit.Record(node.Id, AuditEventType.StageSucceeded, "Execution",
            $"Produced {outcome.Artifacts.Count} artifact(s): {string.Join(", ", outcome.Artifacts.Select(a => a.Name))}.");
        return new StageRunResult(StageStatus.Succeeded, StopKind.None, null);
    }

    private static bool PublishAndClearExitGates(StageNode node, StageOutcome outcome, Blackboard blackboard, AuditLog audit)
    {
        foreach (var a in outcome.Artifacts) blackboard.PutArtifact(a);
        // Record output fingerprints so a later re-plan can detect upstream changes.
        foreach (var a in outcome.Artifacts) blackboard.SetFact($"fp::{a.Name}", a.Fingerprint);

        var gateCtx = new GateContext(node.Id, blackboard, audit);
        foreach (var gate in node.ExitGates)
        {
            var r = gate.Evaluate(gateCtx);
            audit.Record(node.Id, AuditEventType.GateEvaluated, "Gate",
                $"exit:{gate.Name} -> {(r.Passed ? "PASS" : "FAIL")} ({r.Reason})");
            if (!r.Passed) return false;
        }
        return true;
    }

    private static async Task<StageOutcome> SafeExecuteAsync(IStageAgent agent, StageContext ctx)
    {
        try
        {
            return await agent.ExecuteAsync(ctx);
        }
        catch (Exception ex)
        {
            return StageOutcome.Fail($"unhandled exception: {ex.Message}");
        }
    }

    private async Task RollbackAsync(
        StageGraph graph, List<string> completed, Dictionary<string, StageStatus> statuses,
        Blackboard blackboard, AuditLog audit, ReliabilityMetrics metrics, CancellationToken ct)
    {
        audit.Record("pipeline", AuditEventType.RollbackStarted, "Rollback",
            $"Rolling back {completed.Count} completed stage(s) in reverse order.");
        for (var i = completed.Count - 1; i >= 0; i--)
        {
            var node = graph[completed[i]];
            if (node.Rollback is null) continue;
            var ctx = new StageContext(node.Id, blackboard, 0, audit, ct);
            try
            {
                await node.Rollback.RollbackAsync(ctx);
                statuses[node.Id] = StageStatus.RolledBack;
                metrics.MarkRollback();
                audit.Record(node.Id, AuditEventType.RollbackCompleted, "Rollback", "Compensating action applied.");
            }
            catch (Exception ex)
            {
                audit.Record(node.Id, AuditEventType.RollbackCompleted, "Rollback", $"Rollback FAILED: {ex.Message}");
            }
        }
    }

    private bool TryReplan(
        StageGraph graph, Blackboard blackboard, Dictionary<string, StageStatus> statuses,
        AuditLog audit, ReliabilityMetrics metrics, List<string> completed, ref int replanCount)
    {
        if (_replanPolicy is null || replanCount >= _options.MaxReplans) return false;

        var decision = _replanPolicy.Evaluate(graph, blackboard, statuses);
        if (decision is null) return false;

        replanCount++;
        metrics.MarkReplan();

        var invalidated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in decision.StagesToInvalidate)
        {
            invalidated.Add(stage);
            foreach (var dep in graph.TransitiveDependentsOf(stage))
                invalidated.Add(dep);
        }

        foreach (var id in invalidated)
        {
            statuses[id] = StageStatus.Pending;
            completed.Remove(id);
        }

        audit.Record("pipeline", AuditEventType.Replan, "Replan",
            $"{decision.TriggeringReason}. Re-planning stages: {string.Join(", ", invalidated)}.");
        audit.RecordDecision("pipeline", "Re-plan downstream",
            $"upstream change: {decision.TriggeringReason}; invalidated {invalidated.Count} stage(s)");
        return true;
    }
}
