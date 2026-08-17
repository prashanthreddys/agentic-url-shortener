using UrlShortener.Orchestration.Governance;

namespace UrlShortener.Orchestration.Runner.Framework;

/// <summary>
/// Auto-approves high-impact actions under a stated policy and echoes the decision to the console,
/// standing in for a human reviewer. Swap for an interactive handler to require real sign-off.
/// </summary>
public sealed class ConsoleApprovalHandler : IApprovalHandler
{
    private readonly string _approver;
    public ConsoleApprovalHandler(string approver = "Release Manager (policy auto-approve)") => _approver = approver;

    public ApprovalDecision Request(ApprovalRequest request)
    {
        Console.WriteLine($"      >> APPROVAL REQUIRED for '{request.StageId}' (impact={request.Impact}): {request.Reason}");
        Console.WriteLine($"         approved by {_approver}.");
        return ApprovalDecision.Approve(_approver, $"{request.Impact}-impact action authorized");
    }
}
