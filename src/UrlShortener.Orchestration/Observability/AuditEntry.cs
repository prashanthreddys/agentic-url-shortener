namespace UrlShortener.Orchestration.Observability;

public enum AuditEventType
{
    PipelineStarted,
    StageReady,
    GateEvaluated,
    GuardrailEvaluated,
    ApprovalRequested,
    ApprovalDecided,
    StageStarted,
    StageAttempt,
    StageSucceeded,
    StageFailed,
    FallbackInvoked,
    RollbackStarted,
    RollbackCompleted,
    SafeStop,
    Replan,
    Decision,
    PipelineCompleted
}

/// <summary>A single append-only, timestamped record in the audit trail.</summary>
public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string CorrelationId,
    string Stage,
    AuditEventType Type,
    string Category,
    string Message,
    int? Attempt = null)
{
    public override string ToString()
    {
        var attempt = Attempt is { } a ? $" (attempt {a})" : string.Empty;
        return $"{Timestamp:HH:mm:ss.fff} [{Category,-13}] {Stage,-14} {Type}{attempt}: {Message}";
    }
}
