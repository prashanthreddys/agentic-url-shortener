using UrlShortener.Orchestration;
using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Gates;
using UrlShortener.Orchestration.Governance;
using UrlShortener.Orchestration.Graph;
using UrlShortener.Orchestration.Replanning;
using UrlShortener.Orchestration.Runner.Framework;
using UrlShortener.Orchestration.Runner.Llm;

namespace UrlShortener.Orchestration.Runner.Scenarios;

/// <summary>
/// AMBIGUOUS (REAL LLM agents): an under-specified request ("make the links smarter"). The design
/// entry gate holds the pipeline until a human clarifies (delivered via a re-plan that re-runs
/// requirements). Downstream, a security guardrail catches an unresolved open-redirect risk at
/// release and triggers a safe-stop with rollback. Every stage is a real LLM agent; only the
/// governance decides when work may proceed, pause, or unwind.
/// </summary>
public static class LlmAmbiguousScenario
{
    public static Scenario Build(ILlmClient llm, string requirement)
    {
        var requirements = new StageNodeBuilder("requirements")
            .Describe("LLM agent: interpret intent; the pipeline holds if it is under-specified.")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "requirements",
                $"The stakeholder request is: \"{requirement}\". It is vague. " +
                "If it is still ambiguous, list the open questions a product owner must answer " +
                "(for example: does 'smarter' mean device/geo-aware redirect, click-limit expiry, or A/B split?). " +
                "If clarification has been provided, turn it into a concrete requirements spec instead. " +
                "Keep it under 180 words.",
                inputArtifacts: Array.Empty<string>(),
                outputName: "requirements.md", outputKind: "spec",
                // The design gate opens only once a human clarification has arrived (via re-plan).
                onSuccess: ctx => ctx.Blackboard.SetFact(
                    "requirements.clarified", ctx.Blackboard.GetFact<bool>("clarification.received"))))
            .Build();

        var design = new StageNodeBuilder("design")
            .Describe("LLM agent: design smart-redirect rules (blocked until requirements are clarified).")
            .DependsOn("requirements")
            .EntryGate(DelegateGate.RequiresArtifact("requirements.md"),
                       DelegateGate.RequiresFact("requirements.clarified"))
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "architecture/design",
                "Design a rule evaluator on the redirect path that selects a destination by device/geo " +
                "rules and enforces a click-limit expiry. Note the open-redirect security risk it introduces. " +
                "Keep it under 220 words.",
                inputArtifacts: new[] { "requirements.md" },
                outputName: "architecture.md", outputKind: "design"))
            .WithRollback(new DelegateRollback(_ => { }))
            .Build();

        var implementation = new StageNodeBuilder("implementation")
            .Describe("LLM agent: implement the smart-redirect rule evaluator.")
            .DependsOn("design")
            .EntryGate(DelegateGate.RequiresArtifact("architecture.md"))
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "implementation",
                "Generate a concise C# code skeleton for the rule evaluator and multi-destination redirect.",
                inputArtifacts: new[] { "architecture.md" },
                outputName: "code", outputKind: "code"))
            .WithRollback(new DelegateRollback(_ => { }))
            .Build();

        var testing = new StageNodeBuilder("testing")
            .Describe("LLM agent: test rule evaluation and expiry.")
            .DependsOn("implementation")
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "testing",
                "Write a test plan for rule evaluation and click-limit expiry. End with 'VERDICT: PASS'.",
                inputArtifacts: new[] { "code" },
                outputName: "test-report", outputKind: "report",
                onSuccess: ctx => ctx.Blackboard.SetFact("tests.green", true)))
            .ExitGate(DelegateGate.RequiresFact("tests.green"))
            .WithRollback(new DelegateRollback(_ => { }))
            .Build();

        var release = new StageNodeBuilder("release")
            .Describe("LLM agent: release readiness (security risk-control gate denies).")
            .DependsOn("testing")
            .Guardrail(new DelegateGuardrail("open-redirect-review", GuardrailCategory.Security,
                _ => GuardrailResult.Deny(
                    "multi-destination redirect enables an open-redirect / phishing vector; destination allow-listing is not yet implemented")))
            .WithRetry(RetryPolicy.Bounded(2))
            .Runs(new LlmStageAgent(llm, "release",
                "Write release notes for the smart-redirect feature.",
                inputArtifacts: new[] { "requirements.md", "test-report" },
                outputName: "release-notes", outputKind: "release"))
            .Build();

        var graph = StageGraph.Create(new[] { requirements, design, implementation, testing, release });

        // Human clarification arrives as a re-plan: inject the answer and re-run requirements.
        var replan = new DelegateReplanPolicy((g, bb, statuses) =>
        {
            if (statuses["design"] == StageStatus.Blocked && !bb.HasFact("clarification.received"))
            {
                bb.SetFact("clarification.received", true);
                return new ReplanDecision(
                    "product owner clarified: 'smarter' = device-aware redirect + auto-expiry after N clicks",
                    new[] { "requirements" });
            }
            return null;
        });

        return new Scenario(
            "Make the links 'smarter' (ambiguous request, REAL LLM agents)",
            requirement,
            "ambiguous", graph, new ConsoleApprovalHandler(), replan, new OrchestratorOptions());
    }
}
