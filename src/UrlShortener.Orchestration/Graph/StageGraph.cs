namespace UrlShortener.Orchestration.Graph;

/// <summary>
/// An explicit dependency graph (DAG) of SDLC stages. Validates on construction (no missing
/// dependencies, no cycles) and exposes a topological order plus transitive-dependent lookups used
/// by the orchestrator for scheduling and re-planning.
/// </summary>
public sealed class StageGraph
{
    private readonly Dictionary<string, StageNode> _stages;

    public IReadOnlyDictionary<string, StageNode> Stages => _stages;

    private StageGraph(Dictionary<string, StageNode> stages) => _stages = stages;

    public static StageGraph Create(IEnumerable<StageNode> stages)
    {
        var map = new Dictionary<string, StageNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in stages)
        {
            if (!map.TryAdd(s.Id, s))
                throw new InvalidOperationException($"Duplicate stage id '{s.Id}'.");
        }

        foreach (var s in map.Values)
        {
            foreach (var dep in s.DependsOn)
            {
                if (!map.ContainsKey(dep))
                    throw new InvalidOperationException($"Stage '{s.Id}' depends on unknown stage '{dep}'.");
            }
        }

        var graph = new StageGraph(map);
        graph.EnsureAcyclic();
        return graph;
    }

    public StageNode this[string id] => _stages[id];

    /// <summary>Kahn's algorithm; also serves as the cycle check (throws if a cycle remains).</summary>
    public IReadOnlyList<string> TopologicalOrder()
    {
        var inDegree = _stages.Keys.ToDictionary(k => k, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var s in _stages.Values)
            foreach (var _ in s.DependsOn)
                inDegree[s.Id]++;

        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<string>();

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var dependent in _stages.Values.Where(s => s.DependsOn.Contains(id, StringComparer.OrdinalIgnoreCase)))
            {
                if (--inDegree[dependent.Id] == 0)
                    queue.Enqueue(dependent.Id);
            }
        }

        if (order.Count != _stages.Count)
            throw new InvalidOperationException("Dependency graph contains a cycle.");

        return order;
    }

    private void EnsureAcyclic() => TopologicalOrder();

    /// <summary>Direct dependents (stages that list <paramref name="id"/> in their DependsOn).</summary>
    public IEnumerable<StageNode> DirectDependentsOf(string id) =>
        _stages.Values.Where(s => s.DependsOn.Contains(id, StringComparer.OrdinalIgnoreCase));

    /// <summary>All transitive dependents of a stage, in topological order.</summary>
    public IReadOnlyList<string> TransitiveDependentsOf(string id)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(id);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var dep in DirectDependentsOf(current))
            {
                if (result.Add(dep.Id))
                    stack.Push(dep.Id);
            }
        }
        return TopologicalOrder().Where(result.Contains).ToList();
    }
}
