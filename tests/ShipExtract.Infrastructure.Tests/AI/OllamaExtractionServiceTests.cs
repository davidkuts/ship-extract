using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.AI;
using ShipExtract.Infrastructure.Settings;

namespace ShipExtract.Infrastructure.Tests.AI;

/// <summary>Unit tests for <see cref="OllamaExtractionService"/>.</summary>
public sealed class OllamaExtractionServiceTests
{
    private readonly Mock<ILoggingService> _logger = new();

    private static AppSettings DefaultSettings => new()
    {
        OllamaBaseUrl = "http://localhost:11434",
        OllamaModel   = "llama3.1",
        AiProvider    = AiProvider.Ollama
    };

    private static string ValidShipmentJson => JsonSerializer.Serialize(new
    {
        trackingNumber  = "TRACK123",
        consigneeName   = "Test Corp",
        confidenceScore = 0.9,
        documentType    = "AirWaybill",
        shipperName     = (string?)null,
        shipperAddress  = (string?)null,
        shipperCity     = (string?)null,
        shipperCountry  = (string?)null,
        shipperPostalCode = (string?)null,
        consigneeAddress  = (string?)null,
        consigneeCity     = (string?)null,
        consigneeCountry  = (string?)null,
        consigneePostalCode = (string?)null,
        houseBillNumber   = (string?)null,
        masterBillNumber  = (string?)null,
        carrierName       = (string?)null,
        serviceType       = (string?)null,
        shipDate          = (string?)null,
        estimatedDeliveryDate = (string?)null,
        numberOfPieces    = (int?)null,
        grossWeightKg     = (decimal?)null,
        volumeM3          = (decimal?)null,
        description       = (string?)null,
        hsCode            = (string?)null,
        declaredValue     = (decimal?)null,
        currency          = (string?)null,
        freightCost       = (decimal?)null
    });

    private static HttpClient CreateMockClient(params (string url, HttpStatusCode code, string body)[] responses)
    {
        var handler = new MockHttpMessageHandler(responses);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task OllamaNotRunning_ReturnsFailed()
    {
        var handler = new FailingHttpMessageHandler();
        var client  = new HttpClient(handler);
        var svc     = new OllamaExtractionService(client, DefaultSettings, _logger.Object);

        var result = await svc.ExtractAsync("test text");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().ContainAny("not running", "Ollama");
    }

    [Fact]
    public async Task ValidJsonResponse_ReturnsSuccess()
    {
        var ollamaResp = JsonSerializer.Serialize(new { response = ValidShipmentJson, done = true });
        var client = CreateMockClient(
            ("/api/tags",     HttpStatusCode.OK, """{"models":[{"name":"llama3.1:latest"}]}"""),
            ("/api/generate", HttpStatusCode.OK, ollamaResp));

        var svc    = new OllamaExtractionService(client, DefaultSettings, _logger.Object);
        var result = await svc.ExtractAsync("some document text");

        result.Success.Should().BeTrue();
        result.Record.Should().NotBeNull();
        result.Record!.TrackingNumber.Should().Be("TRACK123");
    }

    [Fact]
    public async Task InvalidJsonResponse_RetriesOnce_ThenFails()
    {
        var ollamaResp = JsonSerializer.Serialize(new { response = "NOT JSON AT ALL", done = true });
        var handler    = new CountingMockHandler(
            tagsBody:     """{"models":[{"name":"llama3.1:latest"}]}""",
            generateBody: ollamaResp);
        var client = new HttpClient(handler);

        var svc    = new OllamaExtractionService(client, DefaultSettings, _logger.Object);
        var result = await svc.ExtractAsync("some document text");

        result.Success.Should().BeFalse();
        handler.GenerateCallCount.Should().Be(2); // original + repair retry
    }

    [Fact]
    public async Task EmptyOrInvalidBaseUrl_ReturnsFailed()
    {
        var settings = new AppSettings { OllamaBaseUrl = "", OllamaModel = "llama3.1" };
        var client   = new HttpClient();
        var svc      = new OllamaExtractionService(client, settings, _logger.Object);

        var result = await svc.ExtractAsync("test");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }
}

// ── Test helpers ──────────────────────────────────────────────────────────────

file sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(string Url, HttpStatusCode Code, string Body)> _responses;

    public MockHttpMessageHandler(params (string, HttpStatusCode, string)[] responses)
    {
        _responses = new Queue<(string, HttpStatusCode, string)>(responses);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (_responses.Count == 0)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var (_, code, body) = _responses.Dequeue();
        return Task.FromResult(new HttpResponseMessage(code)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }
}

file sealed class FailingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct) =>
        throw new HttpRequestException("Connection refused");
}

file sealed class CountingMockHandler : HttpMessageHandler
{
    private readonly string _tagsBody;
    private readonly string _generateBody;
    public int GenerateCallCount { get; private set; }

    public CountingMockHandler(string tagsBody, string generateBody)
    {
        _tagsBody     = tagsBody;
        _generateBody = generateBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri?.AbsolutePath.EndsWith("/api/tags") == true)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_tagsBody, Encoding.UTF8, "application/json")
            });

        GenerateCallCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_generateBody, Encoding.UTF8, "application/json")
        });
    }
}
