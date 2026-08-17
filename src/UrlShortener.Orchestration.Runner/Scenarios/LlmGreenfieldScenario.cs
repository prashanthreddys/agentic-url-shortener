using UrlShortener.Orchestration;
using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Gates;
using UrlShortener.Orchestration.Governance;
using UrlShortener.Orchestration.Graph;
using UrlShortener.Orchestration.Runner.Framework;
using UrlShortener.Orchestration.Runner.Llm;

namespace UrlShortener.Orchestration.Runner.Scenarios;

/// <summary>
/// Greenfield pipeline: most SDLC stages are <see cref="LlmStageAgent"/> agents backed by a local
/// model, while the implementation stage emits a deterministic, validated project scaffold via
/// <see cref="ScaffoldAgent"/>. The governance (entry/exit gates, security guardrail, human approval,
/// bounded retries, parallel test/documentation wave) wraps every stage identically.
/// </summary>
public static class LlmGreenfieldScenario
{
    public static Scenario Build(ILlmClient llm, string requirement)
    {
        var requirements = new StageNodeBuilder("requirements")
            .Describe("LLM agent: turn the request into a concrete requirements spec.")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "requirements",
                $"The stakeholder request is: \"{requirement}\". " +
                "Write a short requirements spec: list functional requirements (F1..) and " +
                "non-functional requirements (NFR1..). Keep it under 200 words.",
                inputArtifacts: Array.Empty<string>(),
                outputName: "requirements.md", outputKind: "spec",
                onSuccess: ctx => ctx.Blackboard.SetFact("requirements.clarified", true)))
            .Build();

        var design = new StageNodeBuilder("design")
            .Describe("LLM agent: produce architecture and an API sketch.")
            .DependsOn("requirements")
            .EntryGate(DelegateGate.RequiresArtifact("requirements.md"),
                       DelegateGate.RequiresFact("requirements.clarified"))
            .Guardrail(new DelegateGuardrail("threat-model-reviewed", GuardrailCategory.Security,
                _ => GuardrailResult.Allow("security considerations requested in the design prompt")))
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "architecture/design",
                "Design the system: components, data model, and a REST API sketch (endpoints + methods). " +
                "Note one security consideration (e.g. SSRF or open-redirect). Keep it under 250 words.",
                inputArtifacts: new[] { "requirements.md" },
                outputName: "architecture.md", outputKind: "design"))
            .Build();

        var implementation = new StageNodeBuilder("implementation")
            .Describe("Emit the validated, runnable project scaffold (SCAFFOLD_UrlShortener.md).")
            .DependsOn("design")
            .EntryGate(DelegateGate.RequiresArtifact("architecture.md"))
            .Runs(new ScaffoldAgent())
            .Build();

        var testing = new StageNodeBuilder("testing")
            .Describe("LLM agent: propose a test plan and report a verdict.")
            .DependsOn("implementation")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "testing",
                "Write a test plan: list concrete unit/integration test cases for the code, then end " +
                "with a line 'VERDICT: PASS'. Keep it under 200 words.",
                inputArtifacts: new[] { ScaffoldAgent.OutputName },
                outputName: "test-report", outputKind: "report",
                onSuccess: ctx => ctx.Blackboard.SetFact("tests.green", true)))
            .ExitGate(DelegateGate.RequiresFact("tests.green"))
            .Build();

        var documentation = new StageNodeBuilder("documentation")
            .Describe("LLM agent: write API docs (runs parallel with testing).")
            .DependsOn("implementation")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "documentation",
                "Write a short API reference / README section for the endpoints in the design. " +
                "Keep it under 200 words.",
                inputArtifacts: new[] { "architecture.md", ScaffoldAgent.OutputName },
                outputName: "docs", outputKind: "docs"))
            .Build();

        var release = new StageNodeBuilder("release")
            .Describe("LLM agent: draft release notes (high-impact, human-approved).")
            .DependsOn("testing", "documentation")
            .EntryGate(DelegateGate.RequiresFact("tests.green"), DelegateGate.RequiresArtifact("docs"))
            .Guardrail(new DelegateGuardrail("change-control", GuardrailCategory.ChangeControl,
                _ => GuardrailResult.Allow("release window open; rollback plan attached")))
            .RequireApproval(ImpactLevel.High)
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "release",
                "Write concise v1.0.0 release notes summarizing what was built.",
                inputArtifacts: new[] { "requirements.md", "test-report" },
                outputName: "release-notes", outputKind: "release"))
            .Build();

        var graph = StageGraph.Create(new[] { requirements, design, implementation, testing, documentation, release });
        return new Scenario(
            "Build a URL shortener from scratch (REAL LLM agents via Ollama)",
            requirement,
            "greenfield", graph, new ConsoleApprovalHandler(), Replan: null, new OrchestratorOptions());
    }
}
