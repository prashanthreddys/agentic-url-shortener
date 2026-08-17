using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace UrlShortener.Orchestration.Runner.Llm;

/// <summary>
/// Talks to a local Ollama server (default http://localhost:11434) via its /api/generate endpoint.
/// No API key, runs fully offline once a model is pulled (e.g. `ollama pull llama3.2`).
/// </summary>
public sealed class OllamaClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OllamaClient(string baseUrl, string model)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(10) };
        _model = model;
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var request = new OllamaRequest(_model, $"{systemPrompt}\n\n{userPrompt}", false);
        using var response = await _http.PostAsJsonAsync("/api/generate", request, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
        return body?.Response?.Trim() ?? string.Empty;
    }

    /// <summary>True when the server is reachable and (if pulled) lists the configured model.</summary>
    public async Task<(bool Reachable, bool HasModel, string Detail)> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var tags = await _http.GetFromJsonAsync<OllamaTags>("/api/tags", ct);
            var models = tags?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
            var hasModel = models.Any(m => m == _model || m.StartsWith(_model + ":"));
            return (true, hasModel, hasModel ? $"model '{_model}' available" : $"available models: {string.Join(", ", models)}");
        }
        catch (Exception ex)
        {
            return (false, false, ex.Message);
        }
    }

    private sealed record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record OllamaResponse([property: JsonPropertyName("response")] string? Response);

    private sealed record OllamaTags([property: JsonPropertyName("models")] List<OllamaModel>? Models);

    private sealed record OllamaModel([property: JsonPropertyName("name")] string Name);
}
