using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Gates;
using UrlShortener.Orchestration.Governance;
using UrlShortener.Orchestration.Graph;
using UrlShortener.Orchestration.Replanning;

namespace UrlShortener.Orchestration.Tests;

public class OrchestratorTests
{
    private static StageNodeBuilder Stage(string id) => new(id);

    [Fact]
    public async Task Happy_path_completes_all_stages()
    {
        var graph = StageGraph.Create(new[]
        {
            Stage("a").Runs(new TestAgent(_ => StageOutcome.Ok(TestArtifacts.Named("a")))).Build(),
            Stage("b").DependsOn("a")
                .EntryGate(DelegateGate.RequiresArtifact("a"))
                .Runs(new TestAgent(_ => StageOutcome.Ok(TestArtifacts.Named("b")))).Build(),
        });

        var result = await new Orchestrator(new AutoApprove()).RunAsync(graph);

        Assert.Equal(PipelineStatus.Succeeded, result.Status);
        Assert.All(result.StageStatuses.Values, s => Assert.Equal(StageStatus.Succeeded, s));
        Assert.Equal(1.0, result.Metrics.SuccessRate);
    }

    [Fact]
    public async Task Entry_gate_block_holds_downstream()
    {
        var graph = StageGraph.Create(new[]
        {
            Stage("a").Runs(new TestAgent(_ => StageOutcome.Ok())).Build(), // produces no artifact
            Stage("b").DependsOn("a")
                .EntryGate(DelegateGate.RequiresArtifact("missing"))
                .Runs(new TestAgent(_ => StageOutcome.Ok())).Build(),
        });

        var result = await new Orchestrator(new AutoApprove()).RunAsync(graph);

        Assert.Equal(PipelineStatus.PartiallyCompleted, result.Status);
        Assert.Equal(StageStatus.Blocked, result.StageStatuses["b"]);
    }

    [Fact]
    public async Task Bounded_retry_recovers_transient_failure()
    {
        var attempts = 0;
        var graph = StageGraph.Create(new[]
        {
            Stage("a")
                .WithRetry(RetryPolicy.Bounded(3))
                .Runs(new TestAgent(_ =>
                {
                    attempts++;
                    return attempts < 2 ? StageOutcome.Fail("transient") : StageOutcome.Ok(TestArtifacts.Named("a"));
                })).Build(),
        });

        var result = await new Orchestrator(new AutoApprove()).RunAsync(graph);

        Assert.Equal(PipelineStatus.Succeeded, result.Status);
        Assert.Equal(2, attempts);
        Assert.Equal(1, result.Metrics.Retries);
    }

    [Fact]
    public async Task Terminal_failure_triggers_rollback_and_safe_stop()
    {
        var rolledBack = false;
        var graph = StageGraph.Create(new[]
        {
            Stage("a")
                .Runs(new TestAgent(_ => StageOutcome.Ok(TestArtifacts.Named("a"))))
                .WithRollback(new DelegateRollback(_ => rolledBack = true)).Build(),
            Stage("b").DependsOn("a")
                .Runs(new TestAgent(_ => StageOutcome.Fail("boom"))).Build(),
        });

        var result = await new Orchestrator(new AutoApprove()).RunAsync(graph);

        Assert.Equal(PipelineStatus.SafeStopped, result.Status);
        Assert.True(rolledBack);
        Assert.Equal(StageStatus.RolledBack, result.StageStatuses["a"]);
        Assert.Equal(1, result.Metrics.Rollbacks);
    }

    [Fact]
    public async Task Rejected_approval_safe_stops_without_rollback()
    {
        var rolledBack = false;
        var graph = StageGraph.Create(new[]
        {
            Stage("a")
                .Runs(new TestAgent(_ => StageOutcome.Ok(TestArtifacts.Named("a"))))
                .WithRollback(new DelegateRollback(_ => rolledBack = true)).Build(),
            Stage("b").DependsOn("a")
                .RequireApproval(ImpactLevel.High)
                .Runs(new TestAgent(_ => StageOutcome.Ok(TestArtifacts.Named("b")))).Build(),
        });

        var result = await new Orchestrator(new AlwaysReject()).RunAsync(graph);

        Assert.Equal(PipelineStatus.SafeStopped, result.Status);
        Assert.False(rolledBack); // nothing executed on the rejected stage, prior work preserved
        Assert.Equal(1, result.Metrics.ApprovalsRejected);
        Assert.Equal(StageStatus.Succeeded, result.StageStatuses["a"]);
    }

    [Fact]
    public async Task Guardrail_denial_safe_stops_with_rollback()
    {
        var graph = StageGraph.Create(new[]
        {
            Stage("a")
                .Runs(new TestAgent(_ => StageOutcome.Ok(TestArtifacts.Named("a"))))
                .WithRollback(new DelegateRollback(_ => { })).Build(),
            Stage("b").DependsOn("a")
                .Guardrail(new DelegateGuardrail("sec", GuardrailCategory.Security,
                    _ => GuardrailResult.Deny("policy violation")))
                .Runs(new TestAgent(_ => StageOutcome.Ok())).Build(),
        });

        var result = await new Orchestrator(new AutoApprove()).RunAsync(graph);

        Assert.Equal(PipelineStatus.SafeStopped, result.Status);
        Assert.Equal(1, result.Metrics.GuardrailDenials);
        Assert.Equal(StageStatus.RolledBack, result.StageStatuses["a"]);
    }

    [Fact]
    public async Task Replan_reruns_downstream_when_upstream_changes()
    {
        var aRuns = 0;
        var graph = StageGraph.Create(new[]
        {
            Stage("a").Runs(new TestAgent(_ => { aRuns++; return StageOutcome.Ok(TestArtifacts.Named("a")); })).Build(),
            Stage("b").DependsOn("a").Runs(new TestAgent(_ => StageOutcome.Ok(TestArtifacts.Named("b")))).Build(),
        });

        var replan = new DelegateReplanPolicy((g, bb, statuses) =>
        {
            if (statuses["b"] == StageStatus.Succeeded && !bb.HasFact("done"))
            {
                bb.SetFact("done", true);
                return new ReplanDecision("upstream changed", new[] { "a" });
            }
            return null;
        });

        var result = await new Orchestrator(new AutoApprove(), replan).RunAsync(graph);

        Assert.Equal(PipelineStatus.Succeeded, result.Status);
        Assert.Equal(2, aRuns); // a re-ran after the re-plan
        Assert.Equal(1, result.Metrics.Replans);
    }

    [Fact]
    public async Task Independent_stages_run_in_parallel()
    {
        var tracker = new ConcurrencyTracker();
        var graph = StageGraph.Create(new[]
        {
            Stage("a").Runs(new ConcurrencyProbeAgent(tracker)).Build(),
            Stage("b").Runs(new ConcurrencyProbeAgent(tracker)).Build(),
        });

        var result = await new Orchestrator(new AutoApprove()).RunAsync(graph);

        Assert.Equal(PipelineStatus.Succeeded, result.Status);
        Assert.True(tracker.Max >= 2, $"expected parallel execution, observed max concurrency {tracker.Max}");
    }
}
