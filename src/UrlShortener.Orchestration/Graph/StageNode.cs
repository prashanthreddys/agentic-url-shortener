using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Gates;
using UrlShortener.Orchestration.Governance;

namespace UrlShortener.Orchestration.Graph;

/// <summary>
/// A node in the SDLC dependency graph. Bundles the agent that does the work with its governance:
/// dependencies, entry/exit gates, policy guardrails, retry policy, fallback, rollback, and whether
/// a human must approve the (high-impact) action before it runs.
/// </summary>
public sealed class StageNode
{
    public string Id { get; }
    public string Description { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public IStageAgent Agent { get; }
    public IReadOnlyList<IGate> EntryGates { get; }
    public IReadOnlyList<IGate> ExitGates { get; }
    public IReadOnlyList<IPolicyGuardrail> Guardrails { get; }
    public RetryPolicy Retry { get; }
    public IStageAgent? Fallback { get; }
    public IRollbackAction? Rollback { get; }
    public bool RequiresApproval { get; }
    public ImpactLevel Impact { get; }

    internal StageNode(
        string id, string description, IReadOnlyList<string> dependsOn, IStageAgent agent,
        IReadOnlyList<IGate> entryGates, IReadOnlyList<IGate> exitGates,
        IReadOnlyList<IPolicyGuardrail> guardrails, RetryPolicy retry, IStageAgent? fallback,
        IRollbackAction? rollback, bool requiresApproval, ImpactLevel impact)
    {
        Id = id;
        Description = description;
        DependsOn = dependsOn;
        Agent = agent;
        EntryGates = entryGates;
        ExitGates = exitGates;
        Guardrails = guardrails;
        Retry = retry;
        Fallback = fallback;
        Rollback = rollback;
        RequiresApproval = requiresApproval;
        Impact = impact;
    }
}

/// <summary>Fluent builder for a <see cref="StageNode"/>.</summary>
public sealed class StageNodeBuilder
{
    private readonly string _id;
    private string _description = string.Empty;
    private readonly List<string> _dependsOn = new();
    private IStageAgent? _agent;
    private readonly List<IGate> _entryGates = new();
    private readonly List<IGate> _exitGates = new();
    private readonly List<IPolicyGuardrail> _guardrails = new();
    private RetryPolicy _retry = RetryPolicy.None;
    private IStageAgent? _fallback;
    private IRollbackAction? _rollback;
    private bool _requiresApproval;
    private ImpactLevel _impact = ImpactLevel.Low;

    public StageNodeBuilder(string id) => _id = id;

    public StageNodeBuilder Describe(string description) { _description = description; return this; }
    public StageNodeBuilder DependsOn(params string[] ids) { _dependsOn.AddRange(ids); return this; }
    public StageNodeBuilder Runs(IStageAgent agent) { _agent = agent; return this; }
    public StageNodeBuilder EntryGate(params IGate[] gates) { _entryGates.AddRange(gates); return this; }
    public StageNodeBuilder ExitGate(params IGate[] gates) { _exitGates.AddRange(gates); return this; }
    public StageNodeBuilder Guardrail(params IPolicyGuardrail[] guardrails) { _guardrails.AddRange(guardrails); return this; }
    public StageNodeBuilder WithRetry(RetryPolicy retry) { _retry = retry; return this; }
    public StageNodeBuilder WithFallback(IStageAgent fallback) { _fallback = fallback; return this; }
    public StageNodeBuilder WithRollback(IRollbackAction rollback) { _rollback = rollback; return this; }
    public StageNodeBuilder RequireApproval(ImpactLevel impact = ImpactLevel.High)
    {
        _requiresApproval = true;
        _impact = impact;
        return this;
    }

    public StageNode Build()
    {
        if (_agent is null) throw new InvalidOperationException($"Stage '{_id}' has no agent.");
        return new StageNode(_id, _description, _dependsOn, _agent, _entryGates, _exitGates,
            _guardrails, _retry, _fallback, _rollback, _requiresApproval, _impact);
    }
}
