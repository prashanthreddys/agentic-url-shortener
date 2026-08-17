using UrlShortener.Orchestration;
using UrlShortener.Orchestration.Runner.Framework;
using UrlShortener.Orchestration.Runner.Llm;
using UrlShortener.Orchestration.Runner.Reporting;
using UrlShortener.Orchestration.Runner.Scenarios;

// Entry point: runs the governed SDLC orchestration with real LLM (Ollama) agents.
// Every stage is an LLM agent; the orchestrator wraps them in the same gates, guardrails,
// approvals, retries, rollback, and re-planning.
// Usage:  dotnet run -- [greenfield|brownfield|ambiguous|all] [--out <dir>] ["requirement text"]

var argList = args.ToList();

// Optional: --out <dir> chooses where the greenfield scaffold (SCAFFOLD_UrlShortener.md) is written.
string? outPath = null;
var outIdx = argList.FindIndex(a => a.Equals("--out", StringComparison.OrdinalIgnoreCase));
if (outIdx >= 0)
{
    if (outIdx + 1 < argList.Count)
    {
        outPath = argList[outIdx + 1];
        argList.RemoveRange(outIdx, 2);
    }
    else
    {
        argList.RemoveAt(outIdx);
    }
}

var which = (argList.FirstOrDefault() ?? "all").ToLowerInvariant();

var builders = new Dictionary<string, Func<ILlmClient, string, Scenario>>(StringComparer.OrdinalIgnoreCase)
{
    ["greenfield"] = LlmGreenfieldScenario.Build,
    ["brownfield"] = LlmBrownfieldScenario.Build,
    ["ambiguous"] = LlmAmbiguousScenario.Build,
};

if (which != "all" && !builders.ContainsKey(which))
    throw new ArgumentException($"Unknown scenario '{which}'. Use greenfield | brownfield | ambiguous | all.");

var baseUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";
var model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.2";
var requirement = argList.Count > 1
    ? string.Join(' ', argList.Skip(1))
    : "Build a URL shortener with create, redirect, and click-analytics APIs, non-guessable short codes, and reliability + security features.";

Console.WriteLine("Agentic SDLC Orchestration - URL Shortener (REAL LLM agents)");
Console.WriteLine($"Provider: Ollama at {baseUrl}, model '{model}'.");
Console.WriteLine("Controlled autonomy: agents execute inside gate/guardrail/approval boundaries; humans own sign-off.\n");

var client = new OllamaClient(baseUrl, model);
var (reachable, hasModel, detail) = await client.CheckAsync();
if (!reachable)
{
    Console.WriteLine($"Cannot reach Ollama at {baseUrl}. ({detail})");
    Console.WriteLine("Install Ollama from https://ollama.com/download, then run:");
    Console.WriteLine($"  ollama pull {model}");
    Console.WriteLine("  ollama serve   (usually starts automatically)");
    return;
}
if (!hasModel)
{
    Console.WriteLine($"Ollama is reachable but model '{model}' is not pulled. ({detail})");
    Console.WriteLine($"Run:  ollama pull {model}   (or set OLLAMA_MODEL to an installed model)");
    return;
}

var selected = which == "all"
    ? builders.Values.Select(f => f(client, requirement)).ToList()
    : new List<Scenario> { builders[which](client, requirement) };

var summary = new List<(string Title, OrchestrationResult Result)>();

foreach (var scenario in selected)
{
    Console.WriteLine(new string('=', 100));
    Console.WriteLine($"SCENARIO: {scenario.Title}");
    Console.WriteLine($"Requirement: {scenario.Requirement}");
    Console.WriteLine(new string('=', 100));

    var orchestrator = new Orchestrator(scenario.Approvals, scenario.Replan, scenario.Options);
    var result = await orchestrator.RunAsync(scenario.Graph);
    ConsoleReport.Print(scenario, result);

    // Show the actual model-generated artifacts (the report only prints summaries).
    Console.WriteLine(new string('=', 100));
    Console.WriteLine("MODEL-GENERATED ARTIFACTS (full content)");
    Console.WriteLine(new string('=', 100));
    foreach (var artifact in result.Blackboard.Artifacts.OrderBy(a => a.Name))
    {
        Console.WriteLine($"\n----- {artifact.Name} ({artifact.Kind}) " + new string('-', 60));
        Console.WriteLine(artifact.Content);
    }

    var outDir = ArtifactWriter.Write(scenario.Kind, result);
    Console.WriteLine($"\nArtifacts written to: {outDir}\n");

    // If a scaffold was produced (greenfield) and --out was given, drop SCAFFOLD_UrlShortener.md there.
    var scaffold = result.Blackboard.Artifacts.FirstOrDefault(a => a.Kind == "scaffold");
    if (scaffold is not null && outPath is not null)
    {
        Directory.CreateDirectory(outPath);
        var dest = Path.Combine(outPath, ScaffoldAgent.OutputName);
        File.WriteAllText(dest, scaffold.Content);
        Console.WriteLine($"Scaffold written to: {dest}");
        Console.WriteLine("Hand this file to an AI assistant and ask it to run the embedded script to generate the full project.\n");
    }

    summary.Add((scenario.Title, result));
}

Console.WriteLine(new string('=', 100));
Console.WriteLine("RUN SUMMARY");
Console.WriteLine(new string('=', 100));
foreach (var (title, result) in summary)
{
    Console.WriteLine($"  {result.Status,-18} success={result.Metrics.SuccessRate,5:P0}  " +
                      $"retries={result.Metrics.Retries}  rollbacks={result.Metrics.Rollbacks}  " +
                      $"replans={result.Metrics.Replans}  denials={result.Metrics.GuardrailDenials}  | {title}");
}
Console.WriteLine("Note: 'code' artifacts are model-generated skeletons, not guaranteed-compiling projects.");
