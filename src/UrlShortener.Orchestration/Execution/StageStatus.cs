namespace UrlShortener.Orchestration.Execution;

/// <summary>Lifecycle state of a stage within one orchestration run.</summary>
public enum StageStatus
{
    Pending,
    Blocked,          // an entry gate or guardrail refused entry
    AwaitingApproval,
    Running,
    Succeeded,
    Failed,
    RolledBack,
    Skipped
}

/// <summary>Terminal state of the whole pipeline.</summary>
public enum PipelineStatus
{
    Succeeded,
    Failed,
    SafeStopped,
    PartiallyCompleted
}
