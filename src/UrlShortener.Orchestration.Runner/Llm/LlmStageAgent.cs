using System.Text;
using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Observability;

namespace UrlShortener.Orchestration.Runner.Llm;

/// <summary>
/// A real, LLM-backed <see cref="IStageAgent"/>. It builds a prompt from its role, its instruction,
/// and the upstream artifacts on the blackboard, calls the model, and stores the model's output as
/// this stage's artifact. The orchestration engine wraps it with the same gates, guardrails,
/// approvals, retries, rollback, and re-planning as any other agent.
/// </summary>
public sealed class LlmStageAgent : IStageAgent
{
    private readonly ILlmClient _llm;
    private readonly string _role;
    private readonly string _instruction;
    private readonly IReadOnlyList<string> _inputArtifacts;
    private readonly string _outputName;
    private readonly string _outputKind;
    private readonly Action<StageContext>? _onSuccess;

    public LlmStageAgent(
        ILlmClient llm, string role, string instruction, string[] inputArtifacts,
        string outputName, string outputKind, Action<StageContext>? onSuccess = null)
    {
        _llm = llm;
        _role = role;
        _instruction = instruction;
        _inputArtifacts = inputArtifacts;
        _outputName = outputName;
        _outputKind = outputKind;
        _onSuccess = onSuccess;
    }

    public async Task<StageOutcome> ExecuteAsync(StageContext ctx)
    {
        var context = new StringBuilder();
        foreach (var name in _inputArtifacts)
        {
            var artifact = ctx.Blackboard.GetArtifact(name);
            if (artifact is not null)
                context.AppendLine($"### {artifact.Name}").AppendLine(artifact.Content).AppendLine();
        }

        var system = $"You are an autonomous software-engineering agent responsible for the '{_role}' " +
                     "stage of an SDLC pipeline building a URL shortener. Produce concrete, concise " +
                     "engineering output only. No preamble, no disclaimers.";
        var user = _instruction + (context.Length > 0 ? "\n\nContext from previous stages:\n" + context : string.Empty);

        ctx.Audit.Record(ctx.StageId, AuditEventType.StageAttempt, "LLM",
            $"Invoking model for '{_role}' with {_inputArtifacts.Count} upstream artifact(s).", ctx.Attempt);

        var output = await _llm.CompleteAsync(system, user, ctx.CancellationToken);
        if (string.IsNullOrWhiteSpace(output))
            return StageOutcome.Fail("model returned empty output");

        _onSuccess?.Invoke(ctx);

        var summary = Summarize(output);
        return StageOutcome.Ok($"model produced '{_outputName}'",
            new Artifact(_outputName, _outputKind, summary, output));
    }

    private static string Summarize(string text)
    {
        var flat = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return flat.Length <= 90 ? flat : flat[..90] + "...";
    }
}
