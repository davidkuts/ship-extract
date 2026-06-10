using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipExtract.Infrastructure.AI;

/// <summary>Result of an Ollama health check.</summary>
/// <param name="IsRunning">Whether the Ollama server responded.</param>
/// <param name="ModelAvailable">Whether the configured model is present.</param>
/// <param name="ModelName">The exact model name as reported by Ollama, if found.</param>
/// <param name="AvailableModels">All model names reported by the server.</param>
/// <param name="ErrorMessage">Error description when <paramref name="IsRunning"/> is false.</param>
public record OllamaHealthResult(
    bool IsRunning,
    bool ModelAvailable,
    string? ModelName,
    List<string> AvailableModels,
    string? ErrorMessage
);

/// <summary>Checks whether a local Ollama server is running and a specific model is available.</summary>
public interface IOllamaHealthService
{
    /// <summary>Checks the Ollama server health at <paramref name="baseUrl"/>.</summary>
    Task<OllamaHealthResult> CheckAsync(
        string baseUrl,
        string? modelName = null,
        CancellationToken ct = default);
}

/// <summary>Implements <see cref="IOllamaHealthService"/> using <see cref="HttpClient"/>.</summary>
public sealed class OllamaHealthService : IOllamaHealthService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initialises a new instance of <see cref="OllamaHealthService"/>.</summary>
    public OllamaHealthService(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc/>
    public async Task<OllamaHealthResult> CheckAsync(
        string baseUrl,
        string? modelName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new OllamaHealthResult(false, false, null, [],
                "Ollama URL is not configured.");
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

            var resp = await _http.GetAsync($"{baseUrl.TrimEnd('/')}/api/tags", timeoutCts.Token)
                                  .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var body    = await resp.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            var tagsDoc = JsonSerializer.Deserialize<OllamaTagsResponse>(body, JsonOptions);

            var models = tagsDoc?.Models?
                             .Select(m => m.Name ?? string.Empty)
                             .Where(n => !string.IsNullOrEmpty(n))
                             .ToList()
                         ?? [];

            bool modelFound = false;
            string? foundName = null;

            if (!string.IsNullOrWhiteSpace(modelName))
            {
                // Partial match: "llama3.1" matches "llama3.1:latest"
                foundName  = models.FirstOrDefault(m =>
                    m.StartsWith(modelName, StringComparison.OrdinalIgnoreCase));
                modelFound = foundName is not null;
            }

            return new OllamaHealthResult(true, modelFound, foundName, models, null);
        }
        catch (Exception ex)
        {
            var msg = ex is HttpRequestException or OperationCanceledException
                ? $"Cannot connect to Ollama at {baseUrl}. Is it running?"
                : ex.Message;
            return new OllamaHealthResult(false, false, null, [], msg);
        }
    }
}

// ── Internal DTO ─────────────────────────────────────────────────────────────

file sealed class OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelEntry>? Models { get; set; }
}

file sealed class OllamaModelEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
