using UrlShortener.Orchestration.Execution;

namespace UrlShortener.Orchestration.Governance;

public enum ImpactLevel
{
    Low,
    Medium,
    High
}

public sealed class ApprovalRequest
{
    public required string StageId { get; init; }
    public required string Reason { get; init; }
    public ImpactLevel Impact { get; init; }
    public IReadOnlyList<Artifact> Artifacts { get; init; } = Array.Empty<Artifact>();
}

public sealed record ApprovalDecision(bool Approved, string Approver, string Note)
{
    public static ApprovalDecision Approve(string approver, string note = "approved") => new(true, approver, note);
    public static ApprovalDecision Reject(string approver, string note) => new(false, approver, note);
}

/// <summary>
/// Human-in-the-loop approval boundary for high-impact actions. Implementations may prompt a person,
/// auto-approve within a policy, or reject. This is the "controlled autonomy" enforcement point.
/// </summary>
public interface IApprovalHandler
{
    ApprovalDecision Request(ApprovalRequest request);
}
