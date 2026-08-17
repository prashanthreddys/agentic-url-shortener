using UrlShortener.Orchestration.Execution;

namespace UrlShortener.Orchestration.Runner.Framework;

/// <summary>
/// Decorator that injects a single transient failure on a stage's first attempt, then delegates to
/// the real (LLM) agent on retry. This demonstrates the engine's bounded-retry / MTTR recovery with a
/// deterministic, controllable fault; the actual engineering output is still produced by the inner
/// agent once it runs. The fault fires at most once per run (guarded by a blackboard fact), so a later
/// re-plan that re-runs the stage does not re-trigger it.
/// </summary>
public sealed class TransientFaultAgent : IStageAgent
{
    private readonly IStageAgent _inner;
    private readonly string _faultMessage;
    private readonly string _factKey;

    public TransientFaultAgent(IStageAgent inner, string faultMessage)
    {
        _inner = inner;
        _faultMessage = faultMessage;
        _factKey = $"transient-fault-injected:{Guid.NewGuid():N}";
    }

    public Task<StageOutcome> ExecuteAsync(StageContext ctx)
    {
        if (!ctx.Blackboard.HasFact(_factKey))
        {
            ctx.Blackboard.SetFact(_factKey, true);
            return Task.FromResult(StageOutcome.Fail($"transient fault: {_faultMessage}"));
        }
        return _inner.ExecuteAsync(ctx);
    }
}
