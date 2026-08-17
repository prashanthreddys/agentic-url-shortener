namespace UrlShortener.Orchestration.Execution;

/// <summary>Result returned by a stage agent for a single execution attempt.</summary>
public sealed class StageOutcome
{
    public bool Success { get; private init; }
    public string? FailureReason { get; private init; }
    public IReadOnlyList<Artifact> Artifacts { get; private init; } = Array.Empty<Artifact>();

    /// <summary>Optional human-readable note describing what the agent decided/did.</summary>
    public string? Note { get; private init; }

    public static StageOutcome Ok(string? note, params Artifact[] artifacts) =>
        new() { Success = true, Note = note, Artifacts = artifacts };

    public static StageOutcome Ok(params Artifact[] artifacts) =>
        new() { Success = true, Artifacts = artifacts };

    public static StageOutcome Fail(string reason) =>
        new() { Success = false, FailureReason = reason };
}
