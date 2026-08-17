using UrlShortener.Orchestration.Gates;

namespace UrlShortener.Orchestration.Governance;

public enum GuardrailCategory
{
    Security,
    Compliance,
    ChangeControl
}

public sealed record GuardrailResult(bool Allowed, string Reason, bool RequiresApproval = false)
{
    public static GuardrailResult Allow(string reason = "policy satisfied") => new(true, reason);
    public static GuardrailResult Deny(string reason) => new(false, reason);

    /// <summary>Allowed only if a human approves the high-impact action.</summary>
    public static GuardrailResult NeedsApproval(string reason) => new(true, reason, RequiresApproval: true);
}

/// <summary>
/// A policy guardrail evaluated before a stage runs. Guardrails encode security, compliance, and
/// change-control policy and can hard-deny a stage or escalate it to human approval.
/// </summary>
public interface IPolicyGuardrail
{
    string Name { get; }
    GuardrailCategory Category { get; }
    GuardrailResult Evaluate(GateContext context);
}

/// <summary>A guardrail defined inline from a predicate.</summary>
public sealed class DelegateGuardrail : IPolicyGuardrail
{
    private readonly Func<GateContext, GuardrailResult> _predicate;
    public string Name { get; }
    public GuardrailCategory Category { get; }

    public DelegateGuardrail(string name, GuardrailCategory category, Func<GateContext, GuardrailResult> predicate)
    {
        Name = name;
        Category = category;
        _predicate = predicate;
    }

    public GuardrailResult Evaluate(GateContext context) => _predicate(context);
}
