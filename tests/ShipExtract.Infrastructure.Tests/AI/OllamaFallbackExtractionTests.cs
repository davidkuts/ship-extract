using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Shouldly;
using Moq;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Settings;

namespace ShipExtract.Infrastructure.Tests.AI;

/// <summary>Tests for the Attempt-3 minimal fallback extraction in <see cref="OllamaExtractionService"/>.</summary>
public sealed class OllamaFallbackExtractionTests
{
    private readonly Mock<ILoggingService> _logger = new();

    private static AppSettings DefaultSettings => new()
    {
        OllamaBaseUrl = "http://localhost:11434",
        OllamaModel   = "mistral",
    };

    /// <summary>
    /// Builds a mock Ollama response envelope wrapping the given body string.
    /// </summary>
    private static string OllamaResp(string body) =>
        JsonSerializer.Serialize(new { response = body, done = true });

    /// <summary>
    /// Valid minimal JSON that Attempt 3 should successfully parse.
    /// </summary>
    private static string ValidMinimalJson => JsonSerializer.Serialize(new
    {
        trackingNumber = "FALLBACK-001",
        shipperName    = "Test Shipper",
        consigneeName  = "Test Consignee",
        grossWeightKg  = 12.5,
        declaredValue  = 1000.0,
        currency       = "USD",
    });

    /// <summary>
    /// Invalid JSON that still contains '{' so Attempt 3 is triggered.
    /// </summary>
    private static string InvalidJsonWithBrace => "{\"broken\": }";

    [Fact]
    public async Task FallbackUsed_WhenBothAttemptsFail()
    {
        // tags = OK, attempt1 = invalid JSON with brace, attempt2 = invalid JSON with brace, attempt3 = valid minimal
        var handler = new SequentialMockHandler(
            tagsBody:     """{"models":[{"name":"mistral:latest"}]}""",
            generateBodies: new[]
            {
                OllamaResp(InvalidJsonWithBrace),   // Attempt 1
                OllamaResp(InvalidJsonWithBrace),   // Attempt 2 (repair)
                OllamaResp(ValidMinimalJson),        // Attempt 3 (minimal fallback)
            });

        var client = new HttpClient(handler);
        var svc    = new OllamaExtractionService(client, DefaultSettings, _logger.Object);

        var result = await svc.ExtractAsync("some shipping document text");

        result.Success.ShouldBeTrue();
        result.UsedFallbackExtraction.ShouldBeTrue();
        result.ConfidenceScore.ShouldBe(0.30);
        result.Record.ShouldNotBeNull();
        result.Record!.TrackingNumber.ShouldBe("FALLBACK-001");
    }

    [Fact]
    public async Task AllThreeFail_ReturnsFailedResponse()
    {
        var handler = new SequentialMockHandler(
            tagsBody:     """{"models":[{"name":"mistral:latest"}]}""",
            generateBodies: new[]
            {
                OllamaResp(InvalidJsonWithBrace),   // Attempt 1
                OllamaResp(InvalidJsonWithBrace),   // Attempt 2 (repair)
                OllamaResp(InvalidJsonWithBrace),   // Attempt 3 (minimal fallback — also fails)
            });

        var client = new HttpClient(handler);
        var svc    = new OllamaExtractionService(client, DefaultSettings, _logger.Object);

        var result = await svc.ExtractAsync("some shipping document text");

        result.Success.ShouldBeFalse();
        result.UsedFallbackExtraction.ShouldBeFalse();
    }
}

// ── Test helpers ──────────────────────────────────────────────────────────────

file sealed class SequentialMockHandler : HttpMessageHandler
{
    private readonly string _tagsBody;
    private readonly Queue<string> _generateBodies;

    public SequentialMockHandler(string tagsBody, string[] generateBodies)
    {
        _tagsBody       = tagsBody;
        _generateBodies = new Queue<string>(generateBodies);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri?.AbsolutePath.EndsWith("/api/tags") == true)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_tagsBody, Encoding.UTF8, "application/json")
            });
        }

        // /api/generate
        var body = _generateBodies.Count > 0
            ? _generateBodies.Dequeue()
            : JsonSerializer.Serialize(new { response = "{\"broken\":}", done = true });

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }
}
