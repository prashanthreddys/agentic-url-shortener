using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Observability;

namespace UrlShortener.Orchestration;

/// <summary>The final outcome of an orchestration run: status, per-stage states, metrics, and audit.</summary>
public sealed class OrchestrationResult
{
    public required PipelineStatus Status { get; init; }
    public required IReadOnlyDictionary<string, StageStatus> StageStatuses { get; init; }
    public required ReliabilityMetrics Metrics { get; init; }
    public required AuditLog Audit { get; init; }
    public required Blackboard Blackboard { get; init; }
    public string? StopReason { get; init; }
}
