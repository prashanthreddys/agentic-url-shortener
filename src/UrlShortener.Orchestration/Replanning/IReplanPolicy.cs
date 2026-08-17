using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Graph;

namespace UrlShortener.Orchestration.Replanning;

/// <summary>A decision to invalidate and re-run stages because an upstream input changed.</summary>
public sealed record ReplanDecision(string TriggeringReason, IReadOnlyList<string> StagesToInvalidate);

/// <summary>
/// Evaluated by the orchestrator after each wave. Returns a decision when upstream outputs changed
/// (e.g. a requirement was revised) so downstream stages are re-planned and re-executed while the
/// run stays governed. Implementations must be idempotent enough to fire a bounded number of times.
/// </summary>
public interface IReplanPolicy
{
    ReplanDecision? Evaluate(StageGraph graph, Blackboard blackboard, IReadOnlyDictionary<string, StageStatus> statuses);
}

/// <summary>A replan policy defined inline; typically closes over state to fire only once.</summary>
public sealed class DelegateReplanPolicy : IReplanPolicy
{
    private readonly Func<StageGraph, Blackboard, IReadOnlyDictionary<string, StageStatus>, ReplanDecision?> _fn;

    public DelegateReplanPolicy(Func<StageGraph, Blackboard, IReadOnlyDictionary<string, StageStatus>, ReplanDecision?> fn) => _fn = fn;

    public ReplanDecision? Evaluate(StageGraph graph, Blackboard blackboard, IReadOnlyDictionary<string, StageStatus> statuses) =>
        _fn(graph, blackboard, statuses);
}
