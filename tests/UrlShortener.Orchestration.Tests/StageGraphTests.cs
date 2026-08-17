using UrlShortener.Orchestration.Execution;
using UrlShortener.Orchestration.Graph;

namespace UrlShortener.Orchestration.Tests;

public class StageGraphTests
{
    private static StageNode Node(string id, params string[] deps) =>
        new StageNodeBuilder(id).DependsOn(deps)
            .Runs(new TestAgent(_ => StageOutcome.Ok())).Build();

    [Fact]
    public void TopologicalOrder_respects_dependencies()
    {
        var graph = StageGraph.Create(new[] { Node("c", "b"), Node("b", "a"), Node("a") });

        var order = graph.TopologicalOrder().ToList();

        Assert.True(order.IndexOf("a") < order.IndexOf("b"));
        Assert.True(order.IndexOf("b") < order.IndexOf("c"));
    }

    [Fact]
    public void Missing_dependency_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StageGraph.Create(new[] { Node("a", "does-not-exist") }));
        Assert.Contains("unknown stage", ex.Message);
    }

    [Fact]
    public void Cycle_is_rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StageGraph.Create(new[] { Node("a", "b"), Node("b", "a") }));
        Assert.Contains("cycle", ex.Message);
    }

    [Fact]
    public void TransitiveDependents_are_computed()
    {
        var graph = StageGraph.Create(new[] { Node("a"), Node("b", "a"), Node("c", "b"), Node("d") });

        var dependents = graph.TransitiveDependentsOf("a");

        Assert.Contains("b", dependents);
        Assert.Contains("c", dependents);
        Assert.DoesNotContain("d", dependents);
    }
}
