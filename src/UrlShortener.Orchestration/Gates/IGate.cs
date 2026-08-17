using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Observability;

namespace UrlShortener.Orchestration.Gates;

/// <summary>Read-only context handed to gates and guardrails when they evaluate a stage.</summary>
public sealed class GateContext
{
    public string StageId { get; }
    public Blackboard Blackboard { get; }
    public AuditLog Audit { get; }

    public GateContext(string stageId, Blackboard blackboard, AuditLog audit)
    {
        StageId = stageId;
        Blackboard = blackboard;
        Audit = audit;
    }
}

public sealed record GateResult(bool Passed, string Reason)
{
    public static GateResult Pass(string reason = "conditions met") => new(true, reason);
    public static GateResult Block(string reason) => new(false, reason);
}

/// <summary>
/// An entry or exit gate. Entry gates decide whether a stage may start; exit gates verify a stage's
/// output before downstream work is unblocked. Gates make the graph governed rather than free-running.
/// </summary>
public interface IGate
{
    string Name { get; }
    GateResult Evaluate(GateContext context);
}

/// <summary>A gate defined inline from a predicate. Keeps scenario wiring compact.</summary>
public sealed class DelegateGate : IGate
{
    private readonly Func<GateContext, GateResult> _predicate;
    public string Name { get; }

    public DelegateGate(string name, Func<GateContext, GateResult> predicate)
    {
        Name = name;
        _predicate = predicate;
    }

    public GateResult Evaluate(GateContext context) => _predicate(context);

    /// <summary>Convenience: gate that passes only when the named artifact exists on the blackboard.</summary>
    public static DelegateGate RequiresArtifact(string artifactName) =>
        new($"requires:{artifactName}", ctx => ctx.Blackboard.HasArtifact(artifactName)
            ? GateResult.Pass($"artifact '{artifactName}' present")
            : GateResult.Block($"artifact '{artifactName}' missing"));

    /// <summary>Convenience: gate that passes only when a boolean fact is true.</summary>
    public static DelegateGate RequiresFact(string factKey) =>
        new($"requires-fact:{factKey}", ctx => ctx.Blackboard.GetFact<bool>(factKey)
            ? GateResult.Pass($"fact '{factKey}' is true")
            : GateResult.Block($"fact '{factKey}' is not satisfied"));
}
