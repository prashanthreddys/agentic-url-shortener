using System.Collections.Concurrent;

namespace UrlShortener.Orchestration.Execution;

/// <summary>
/// Shared, thread-safe cross-stage context (a "blackboard"). Preserves artifacts and free-form
/// facts produced by earlier stages so later stages and gates can reason over them. This is what
/// makes execution stateful rather than a stateless task chain.
/// </summary>
public sealed class Blackboard
{
    private readonly ConcurrentDictionary<string, Artifact> _artifacts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, object?> _facts = new(StringComparer.OrdinalIgnoreCase);

    public void PutArtifact(Artifact artifact) => _artifacts[artifact.Name] = artifact;

    public Artifact? GetArtifact(string name) =>
        _artifacts.TryGetValue(name, out var a) ? a : null;

    public bool HasArtifact(string name) => _artifacts.ContainsKey(name);

    public IReadOnlyCollection<Artifact> Artifacts => _artifacts.Values.ToList();

    public void SetFact(string key, object? value) => _facts[key] = value;

    public T? GetFact<T>(string key) =>
        _facts.TryGetValue(key, out var v) && v is T typed ? typed : default;

    public bool HasFact(string key) => _facts.ContainsKey(key);
}
