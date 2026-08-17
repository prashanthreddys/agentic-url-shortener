using UrlShortener.Orchestration;
using UrlShortener.Orchestration.Governance;
using UrlShortener.Orchestration.Graph;
using UrlShortener.Orchestration.Replanning;

namespace UrlShortener.Orchestration.Runner.Framework;

/// <summary>A self-contained, runnable SDLC scenario: the requirement plus its governed graph.</summary>
public sealed record Scenario(
    string Title,
    string Requirement,
    string Kind,
    StageGraph Graph,
    IApprovalHandler Approvals,
    IReplanPolicy? Replan,
    OrchestratorOptions Options);
