namespace UrlShortener.Orchestration.Runner.Llm;

/// <summary>Minimal text-completion abstraction so stage agents are provider-agnostic.</summary>
public interface ILlmClient
{
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
