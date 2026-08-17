using UrlShortener.Orchestration;
using UrlShortener.Orchestration.Execution;

namespace UrlShortener.Orchestration.Runner.Reporting;

/// <summary>
/// Persists a run's produced artifacts to a real folder on disk (output/&lt;timestamp&gt;_&lt;scenario&gt;/),
/// so a greenfield/brownfield run leaves inspectable files (spec, design, code, tests, docs, ...)
/// rather than only in-memory blackboard entries.
/// </summary>
public static class ArtifactWriter
{
    public static string Write(string scenarioKind, OrchestrationResult result)
    {
        var runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "output", $"{runId}_{scenarioKind}");
        Directory.CreateDirectory(dir);

        foreach (var artifact in result.Blackboard.Artifacts)
            File.WriteAllText(Path.Combine(dir, FileNameFor(artifact)), artifact.Content);

        var lines = result.Blackboard.Artifacts
            .OrderBy(a => a.Name)
            .Select(a => $"{a.Name,-24} {a.Kind,-10} -> {FileNameFor(a)}");
        File.WriteAllText(Path.Combine(dir, "MANIFEST.txt"),
            $"Scenario : {scenarioKind}\r\nStatus   : {result.Status}\r\nGenerated: {DateTime.Now:u}\r\n\r\nArtifacts:\r\n" +
            string.Join("\r\n", lines) + "\r\n");

        return dir;
    }

    private static string FileNameFor(Artifact a)
    {
        var name = Sanitize(a.Name);
        if (Path.HasExtension(name)) return name;
        var ext = a.Kind switch
        {
            "code" => ".cs",
            "schema" => ".yaml",
            "migration" => ".sql",
            _ => ".md",
        };
        return name + ext;
    }

    private static string Sanitize(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}
