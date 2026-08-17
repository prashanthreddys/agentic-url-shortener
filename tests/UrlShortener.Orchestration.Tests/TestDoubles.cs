using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Governance;

namespace UrlShortener.Orchestration.Tests;

/// <summary>Stage agent driven by an inline delegate.</summary>
internal sealed class TestAgent : IStageAgent
{
    private readonly Func<StageContext, StageOutcome> _fn;
    public TestAgent(Func<StageContext, StageOutcome> fn) => _fn = fn;
    public Task<StageOutcome> ExecuteAsync(StageContext ctx) => Task.FromResult(_fn(ctx));
}

/// <summary>Async agent that records observed concurrency to verify parallel waves.</summary>
internal sealed class ConcurrencyProbeAgent : IStageAgent
{
    private readonly ConcurrencyTracker _tracker;
    public ConcurrencyProbeAgent(ConcurrencyTracker tracker) => _tracker = tracker;

    public async Task<StageOutcome> ExecuteAsync(StageContext ctx)
    {
        _tracker.Enter();
        await Task.Delay(40, ctx.CancellationToken);
        _tracker.Exit();
        return StageOutcome.Ok(new Artifact(ctx.StageId, "k", "s", "c"));
    }
}

internal sealed class ConcurrencyTracker
{
    private int _current;
    public int Max { get; private set; }
    private readonly object _lock = new();

    public void Enter()
    {
        lock (_lock)
        {
            _current++;
            if (_current > Max) Max = _current;
        }
    }

    public void Exit()
    {
        lock (_lock) _current--;
    }
}

internal sealed class AutoApprove : IApprovalHandler
{
    public ApprovalDecision Request(ApprovalRequest request) => ApprovalDecision.Approve("test-approver");
}

internal sealed class AlwaysReject : IApprovalHandler
{
    public ApprovalDecision Request(ApprovalRequest request) => ApprovalDecision.Reject("test-approver", "not allowed");
}

internal static class TestArtifacts
{
    public static Artifact Named(string name) => new(name, "kind", "summary", $"content-of-{name}");
}
