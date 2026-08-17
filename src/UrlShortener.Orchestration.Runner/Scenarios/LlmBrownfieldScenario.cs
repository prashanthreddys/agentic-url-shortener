using UrlShortener.Orchestration;
using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Gates;
using UrlShortener.Orchestration.Governance;
using UrlShortener.Orchestration.Graph;
using UrlShortener.Orchestration.Replanning;
using UrlShortener.Orchestration.Runner.Framework;
using UrlShortener.Orchestration.Runner.Llm;

namespace UrlShortener.Orchestration.Runner.Scenarios;

public static class LlmBrownfieldScenario
{
    public static Scenario Build(ILlmClient llm, string requirement)
    {
        var requirements = new StageNodeBuilder("requirements")
            .Describe("LLM agent: clarify enhancement request.")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "requirements",
                $"The enhancement request is: \"{requirement}\". " +
                "Write a short, concrete requirements spec.",
                inputArtifacts: Array.Empty<string>(),
                outputName: "requirements.md", outputKind: "spec",
                onSuccess: ctx => ctx.Blackboard.SetFact("requirements.clarified", true)))
            .Build();

        var impact = new StageNodeBuilder("impact-analysis")
            .Describe("LLM agent: codebase reasoning (identify impact).")
            .DependsOn("requirements")
            .EntryGate(DelegateGate.RequiresArtifact("requirements.md"))
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "impact-analysis",
                "Based on the requirements, identify the modules, APIs, and data flows in the existing URL shortener " +
                "that must be modified. List them compactly.",
                inputArtifacts: new[] { "requirements.md" },
                outputName: "impact-map", outputKind: "analysis"))
            .Build();

        var design = new StageNodeBuilder("design")
            .Describe("LLM agent: design the enhancement.")
            .DependsOn("impact-analysis")
            .EntryGate(DelegateGate.RequiresArtifact("impact-map"))
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "design",
                "Design the data model and API changes needed for this enhancement.",
                inputArtifacts: new[] { "impact-map" },
                outputName: "analytics-design.md", outputKind: "design"))
            .Build();

        var migration = new StageNodeBuilder("migration")
            .Describe("LLM agent: generate DB migration.")
            .DependsOn("design")
            .Guardrail(new DelegateGuardrail("schema-change-control", GuardrailCategory.ChangeControl,
                _ => GuardrailResult.NeedsApproval("requires DBA sign-off")))
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "migration",
                "Write a short SQL migration script for the new tables/columns.",
                inputArtifacts: new[] { "analytics-design.md" },
                outputName: "migration.sql", outputKind: "migration"))
            .Build();

        var implementation = new StageNodeBuilder("implementation")
            .Describe("LLM agent: implement changes (first attempt hits a transient fault, recovers on retry).")
            .DependsOn("migration")
            .WithRetry(RetryPolicy.Bounded(2))
            // Inject one transient failure so the bounded-retry / MTTR path is demonstrated; the real
            // LLM agent produces the code on the retry.
            .Runs(new TransientFaultAgent(
                new LlmStageAgent(llm, "implementation",
                    "Write the C# code snippet to implement this enhancement in the service layer.",
                    inputArtifacts: new[] { "analytics-design.md" },
                    outputName: "code-analytics", outputKind: "code"),
                "MongoDB write timeout"))
            .Build();

        var testing = new StageNodeBuilder("testing")
            .Describe("LLM agent: test plan.")
            .DependsOn("implementation")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "testing",
                "Write a regression + new feature test plan. End with 'VERDICT: PASS'.",
                inputArtifacts: new[] { "code-analytics" },
                outputName: "test-report", outputKind: "report",
                onSuccess: ctx => ctx.Blackboard.SetFact("tests.green", true)))
            .ExitGate(DelegateGate.RequiresFact("tests.green"))
            .Build();

        var documentation = new StageNodeBuilder("documentation")
            .Describe("LLM agent: update docs.")
            .DependsOn("implementation")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "documentation",
                "Update the API documentation for the new endpoints.",
                inputArtifacts: new[] { "analytics-design.md", "code-analytics" },
                outputName: "docs", outputKind: "docs"))
            .Build();

        var release = new StageNodeBuilder("release")
            .Describe("LLM agent: release notes.")
            .DependsOn("testing", "documentation")
            .EntryGate(DelegateGate.RequiresFact("tests.green"))
            .RequireApproval(ImpactLevel.High)
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "release",
                "Write v1.1.0 release notes.",
                inputArtifacts: new[] { "requirements.md", "test-report" },
                outputName: "release-notes", outputKind: "release"))
            .Build();

        var replan = new DelegateReplanPolicy((g, bb, statuses) =>
        {
            if (statuses["testing"] == StageStatus.Succeeded && !bb.HasFact("perf.reviewed"))
            {
                bb.SetFact("perf.reviewed", true);
                return new ReplanDecision(
                    "performance review: synchronous analytics write is too slow",
                    new[] { "implementation" });
            }
            return null;
        });

        var graph = StageGraph.Create(new[] { requirements, impact, design, migration, implementation, testing, documentation, release });
        return new Scenario(
            "Add click analytics (REAL LLM agents)",
            requirement,
            "brownfield", graph, new ConsoleApprovalHandler(), replan, new OrchestratorOptions());
    }
}
