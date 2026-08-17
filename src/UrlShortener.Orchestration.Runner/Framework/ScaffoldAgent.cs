using System.Reflection;
using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Observability;

namespace UrlShortener.Orchestration.Runner.Framework;

/// <summary>
/// Deterministic stage agent that emits the validated project scaffold document
/// (SCAFFOLD_UrlShortener.md). Unlike an LLM code agent, its output is fixed and known-good: the
/// embedded markdown contains a script that generates a compiling, runnable project, so a developer
/// (or another model) can use it as a reliable reference rather than regenerating code from scratch.
/// </summary>
public sealed class ScaffoldAgent : IStageAgent
{
    public const string OutputName = "SCAFFOLD_UrlShortener.md";

    public Task<StageOutcome> ExecuteAsync(StageContext ctx)
    {
        var content = LoadEmbeddedScaffold();
        ctx.Audit.Record(ctx.StageId, AuditEventType.StageAttempt, "Scaffold",
            "Emitting validated project scaffold (deterministic, known-good).", ctx.Attempt);
        ctx.Blackboard.SetFact("scaffold.emitted", true);
        return Task.FromResult(StageOutcome.Ok("emitted runnable project scaffold",
            new Artifact(OutputName, "scaffold",
                "Runnable project scaffold: builds, tests pass, API runs.", content)));
    }

    private static string LoadEmbeddedScaffold()
    {
        var asm = typeof(ScaffoldAgent).Assembly;
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith("UrlShortener.Scaffold.md", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
