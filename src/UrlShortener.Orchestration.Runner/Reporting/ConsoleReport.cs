using UrlShortener.Orchestration;
using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Runner.Framework;

namespace UrlShortener.Orchestration.Runner.Reporting;

/// <summary>Renders a scenario's governed run as an audit-grade console report.</summary>
public static class ConsoleReport
{
    public static void Print(Scenario scenario, OrchestrationResult result)
    {
        var rule = new string('=', 100);
        Console.WriteLine();
        Console.WriteLine(rule);
        Console.WriteLine($"SCENARIO ({scenario.Kind.ToUpperInvariant()}): {scenario.Title}");
        Console.WriteLine(rule);
        Console.WriteLine($"Requirement: {scenario.Requirement}");
        Console.WriteLine();

        Console.WriteLine("-- Audit trail (traceability, correlation id = " + result.Audit.CorrelationId + ") ".PadRight(60, '-'));
        foreach (var entry in result.Audit.Entries)
            Console.WriteLine("  " + entry);
        Console.WriteLine();

        Console.WriteLine("-- Decision lineage " + new string('-', 78));
        foreach (var d in result.Audit.Decisions)
            Console.WriteLine($"  {d.Stage,-14} {d.Message}");
        Console.WriteLine();

        Console.WriteLine("-- Stage outcomes " + new string('-', 80));
        foreach (var kv in result.StageStatuses)
            Console.WriteLine($"  {kv.Key,-16} {Describe(kv.Value)}");
        Console.WriteLine();

        Console.WriteLine("-- Reliability metrics " + new string('-', 75));
        var m = result.Metrics;
        Console.WriteLine($"  Pipeline status ........ {result.Status}" + (result.StopReason is { } r ? $"  ({r})" : ""));
        Console.WriteLine($"  Success rate ........... {m.SuccessRate:P0}  ({m.StagesSucceeded}/{m.StagesAttempted} stage executions)");
        Console.WriteLine($"  Retries / frequency .... {m.Retries}  ({m.RetryFrequency:P0})");
        Console.WriteLine($"  Rollbacks / frequency .. {m.Rollbacks}  ({m.RollbackFrequency:P0})");
        Console.WriteLine($"  Re-plans ............... {m.Replans}");
        Console.WriteLine($"  Approvals (req/reject) . {m.ApprovalsRequested} / {m.ApprovalsRejected}");
        Console.WriteLine($"  Guardrail denials ...... {m.GuardrailDenials}");
        Console.WriteLine($"  MTTR ................... {m.MeanTimeToRecovery.TotalMilliseconds:F0} ms");
        Console.WriteLine($"  End-to-end latency ..... {m.EndToEndLatency.TotalMilliseconds:F0} ms");
        Console.WriteLine();

        Console.WriteLine("-- Artifacts produced (final blackboard state) " + new string('-', 51));
        foreach (var a in result.Blackboard.Artifacts.OrderBy(a => a.Name))
            Console.WriteLine($"  [{a.Kind,-11}] {a.Name,-22} fp:{a.Fingerprint}  {a.Summary}");
        Console.WriteLine();
    }

    private static string Describe(StageStatus status) => status switch
    {
        StageStatus.Succeeded => "SUCCEEDED",
        StageStatus.Failed => "FAILED",
        StageStatus.Blocked => "BLOCKED (governance hold)",
        StageStatus.RolledBack => "ROLLED BACK",
        StageStatus.Pending => "PENDING (not reached)",
        _ => status.ToString()
    };
}
