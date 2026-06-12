using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.AI.Models;
using ShipExtract.Infrastructure.Settings;

namespace ShipExtract.Infrastructure.AI;

/// <summary>
/// Implements <see cref="IAiExtractionService"/> using a locally running Ollama server.
/// Uses only <see cref="HttpClient"/> — no external SDK required.
/// </summary>
public sealed class OllamaExtractionService : IAiExtractionService
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;
    private readonly ILoggingService _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan GenerateTimeout    = TimeSpan.FromSeconds(300);

    /// <summary>Initialises a new instance of <see cref="OllamaExtractionService"/>.</summary>
    public OllamaExtractionService(HttpClient httpClient, AppSettings settings, ILoggingService logger)
    {
        _http     = httpClient;
        _settings = settings;
        _logger   = logger;
    }

    /// <inheritdoc/>
    public Task<AiExtractionResponse> ExtractAsync(
        string rawText,
        DocumentType hint,
        CarrierType carrier,
        CancellationToken ct = default) =>
        ExtractCoreAsync(rawText, hint, carrier, null, ct);

    /// <inheritdoc/>
    public Task<AiExtractionResponse> ExtractAsync(
        string rawText,
        DocumentType hint,
        CarrierType carrier,
        List<CustomField>? customFields,
        CancellationToken ct = default) =>
        ExtractCoreAsync(rawText, hint, carrier, customFields, ct);

    /// <inheritdoc/>
    public async Task<AiExtractionResponse> ExtractAsync(
        string rawText,
        DocumentType hint = DocumentType.Unknown,
        CancellationToken ct = default) =>
        await ExtractCoreAsync(rawText, hint, CarrierType.Unknown, null, ct);

    private async Task<AiExtractionResponse> ExtractCoreAsync(
        string rawText,
        DocumentType hint,
        CarrierType carrier,
        List<CustomField>? customFields,
        CancellationToken ct)
    {
        var baseUrl = _settings.OllamaBaseUrl?.Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Ollama base URL is not configured or invalid. Set it in Settings.");
        }

        _logger.LogDebug("Ollama extraction starting — URL: {Url}, Model: {Model}",
            baseUrl, _settings.OllamaModel);

        // Step 1 — reachability check
        try
        {
            using var healthCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            healthCts.CancelAfter(HealthCheckTimeout);
            var healthResp = await _http.GetAsync(
                $"{baseUrl}/api/tags", healthCts.Token).ConfigureAwait(false);
            healthResp.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail("Ollama is not running. Start it with: ollama serve");
        }
        catch (HttpRequestException)
        {
            return Fail($"Ollama is not running at {baseUrl}. Start it with: ollama serve");
        }

        // Step 2 — build combined prompt (Ollama-specific, more explicit than Anthropic version)
        var prompt = ExtractionPromptBuilder.BuildOllamaPrompt(rawText, hint, carrier, customFields);

        // Step 3 — POST to /api/generate
        string rawResponse;
        try
        {
            rawResponse = await GenerateAsync(baseUrl, prompt, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Fail($"Ollama request timed out after {(int)GenerateTimeout.TotalSeconds} seconds. Try a smaller model or reduce document size.");
        }
        catch (HttpRequestException ex)
        {
            return Fail($"Could not connect to Ollama at {baseUrl}. Is it running? ({ex.Message})");
        }

        // Steps 4–8 — parse and optionally repair
        var cleaned = ExtractJsonFromResponse(rawResponse);
        var parsed  = TryParseResponse(cleaned, rawResponse, customFields);
        if (parsed is not null)
        {
            _logger.LogDebug(
                "Ollama extracted: Tracking={Tracking}, Consignee={Consignee}, Shipper={Shipper}, Confidence={Confidence:P0}",
                parsed.Record?.TrackingNumber ?? "<null>",
                parsed.Record?.ConsigneeName  ?? "<null>",
                parsed.Record?.ShipperName    ?? "<null>",
                parsed.ConfidenceScore);
            return parsed;
        }

        _logger.LogWarning("Ollama returned invalid JSON — attempting repair.");

        // Attempt 2 — repair prompt
        var repairPrompt =
            $"""
            The text below is supposed to be a JSON object but has syntax errors.
            Return ONLY the corrected JSON object.
            Rules: use double quotes, no trailing commas, no comments, no markdown.
            Do not add any explanation before or after the JSON.

            Broken JSON:
            {cleaned}
            """;
        try
        {
            var repaired = await GenerateAsync(baseUrl, repairPrompt, ct).ConfigureAwait(false);
            repaired = ExtractJsonFromResponse(repaired);
            var repairResult = TryParseResponse(repaired, rawResponse, customFields);
            if (repairResult is not null) return repairResult;

            // Attempt 3 — minimal fallback prompt.
            // Only triggered when the repair response still has JSON-like structure (contains '{'),
            // indicating the model is trying to produce JSON but keeps generating syntax errors.
            if (repaired.Contains('{'))
            {
                var fallback = await TryMinimalFallbackAsync(baseUrl, rawText, rawResponse, ct)
                    .ConfigureAwait(false);
                if (fallback is not null) return fallback;
            }

            return Fail("Ollama returned invalid JSON that could not be repaired.");
        }
        catch
        {
            return Fail("Ollama returned invalid JSON that could not be repaired.");
        }
    }

    private async Task<AiExtractionResponse?> TryMinimalFallbackAsync(
        string baseUrl, string rawText, string originalRawJson, CancellationToken ct)
    {
        var snippet = rawText.Length > 1000 ? rawText[..1000] : rawText;
        var minimalPrompt =
            $$"""
            Extract these 6 fields from the shipping document text below.
            Return ONLY a JSON object with exactly these keys:

            {
              "trackingNumber": "string or null",
              "shipperName": "string or null",
              "consigneeName": "string or null",
              "grossWeightKg": number or null,
              "declaredValue": number or null,
              "currency": "string or null"
            }

            Document:
            {{snippet}}
            """;

        try
        {
            var minimalResponse = await GenerateAsync(baseUrl, minimalPrompt, ct).ConfigureAwait(false);
            var minimalCleaned  = ExtractJsonFromResponse(minimalResponse);
            var dto = System.Text.Json.JsonSerializer.Deserialize<MinimalFallbackDto>(
                minimalCleaned, JsonOptions);

            if (dto is null) return null;

            var record = new Domain.Models.ShipmentRecord
            {
                TrackingNumber = dto.TrackingNumber,
                ShipperName    = dto.ShipperName,
                ConsigneeName  = dto.ConsigneeName,
                GrossWeightKg  = dto.GrossWeightKg,
                DeclaredValue  = dto.DeclaredValue,
                Currency       = dto.Currency,
                ConfidenceScore = 0.30,
            };

            _logger.LogWarning(
                "Used minimal fallback extraction — only core fields captured. Tracking={Tracking}",
                record.TrackingNumber ?? "<null>");

            return new AiExtractionResponse(record, 0.30, originalRawJson, true, null)
            {
                UsedFallbackExtraction = true
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GenerateAsync(string baseUrl, string prompt, CancellationToken ct)
    {
        var body = new OllamaGenerateRequest
        {
            Model  = _settings.OllamaModel ?? "llama3.1",
            Prompt = prompt,
            Stream = false,
            Format = "json"
        };

        var json    = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(GenerateTimeout);

        _logger.LogDebug("Calling Ollama: POST {Uri}", $"{baseUrl}/api/generate");
        var resp = await _http.PostAsync($"{baseUrl}/api/generate", content, timeoutCts.Token)
                              .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        var parsed   = JsonSerializer.Deserialize<OllamaGenerateResponse>(respJson, JsonOptions);
        return parsed?.Response ?? string.Empty;
    }

    private static string ExtractJsonFromResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        // Step 1: Strip markdown fences
        var text = raw.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence    = text.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                text = text[(firstNewline + 1)..lastFence].Trim();
        }

        // Step 2: Find the outermost { } block
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            text = text[start..(end + 1)];

        // Step 3: Fix common llama3.1 JSON quirks
        // Remove trailing commas before } or ]
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @",\s*([}\]])", "$1");

        // Replace single-quoted property names with double quotes
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"'([^']+)'(\s*:)", "\"$1\"$2");

        return text;
    }

    private static AiExtractionResponse? TryParseResponse(string json, string rawResponse, List<CustomField>? customFields = null)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<ShipmentExtractionDto>(json, JsonOptions);
            return dto is null ? null : BuildSuccess(dto, rawResponse, customFields);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AiExtractionResponse BuildSuccess(ShipmentExtractionDto dto, string rawJson, List<CustomField>? customFields = null)
    {
        var record = new ShipmentRecord
        {
            ShipperName           = dto.ShipperName,
            ShipperAddress        = dto.ShipperAddress,
            ShipperCity           = dto.ShipperCity,
            ShipperCountry        = dto.ShipperCountry,
            ShipperPostalCode     = dto.ShipperPostalCode,
            ConsigneeName         = dto.ConsigneeName,
            ConsigneeAddress      = dto.ConsigneeAddress,
            ConsigneeCity         = dto.ConsigneeCity,
            ConsigneeCountry      = dto.ConsigneeCountry,
            ConsigneePostalCode   = dto.ConsigneePostalCode,
            TrackingNumber        = dto.TrackingNumber,
            HouseBillNumber       = dto.HouseBillNumber,
            MasterBillNumber      = dto.MasterBillNumber,
            CarrierName           = dto.CarrierName,
            ServiceType           = dto.ServiceType,
            ShipDate              = ParseDate(dto.ShipDate),
            EstimatedDeliveryDate = ParseDate(dto.EstimatedDeliveryDate),
            NumberOfPieces        = dto.NumberOfPieces,
            GrossWeightKg         = dto.GrossWeightKg,
            VolumeM3              = dto.VolumeM3,
            Description           = dto.Description,
            HsCode                = dto.HsCode,
            DeclaredValue         = dto.DeclaredValue,
            Currency              = dto.Currency,
            FreightCost           = dto.FreightCost,
            DocumentType          = ParseDocumentType(dto.DocumentType),
        };
        record.ConfidenceScore = ComputeConfidence(record);
        record.CustomFields    = MapCustomFields(dto.CustomFields, customFields);
        return new AiExtractionResponse(record, record.ConfidenceScore, rawJson, true, null);
    }

    private static List<CustomFieldValue> MapCustomFields(
        List<CustomFieldDto>? dtoFields, List<CustomField>? requestedFields)
    {
        var result = new List<CustomFieldValue>();
        if (requestedFields is null || requestedFields.Count == 0) return result;

        foreach (var requested in requestedFields.Where(f => f.IsEnabled && !string.IsNullOrWhiteSpace(f.Name)))
        {
            var matched = dtoFields?.FirstOrDefault(
                d => string.Equals(d.Name, requested.Name, StringComparison.OrdinalIgnoreCase));

            result.Add(new CustomFieldValue
            {
                FieldId   = requested.Id,
                FieldName = requested.Name,
                Value     = matched?.Value ?? requested.DefaultValue
            });
        }

        return result;
    }

    private static double ComputeConfidence(ShipmentRecord record)
    {
        var checks = new (bool present, double weight)[]
        {
            // Identity — highest weight
            (!string.IsNullOrWhiteSpace(record.TrackingNumber)
             || !string.IsNullOrWhiteSpace(record.HouseBillNumber)
             || !string.IsNullOrWhiteSpace(record.MasterBillNumber), 0.25),

            // Parties
            (!string.IsNullOrWhiteSpace(record.ShipperName),   0.15),
            (!string.IsNullOrWhiteSpace(record.ConsigneeName), 0.15),

            // Addresses
            (!string.IsNullOrWhiteSpace(record.ShipperCountry),   0.05),
            (!string.IsNullOrWhiteSpace(record.ConsigneeCountry), 0.05),

            // Cargo
            (record.GrossWeightKg.HasValue,                   0.10),
            (record.NumberOfPieces.HasValue,                  0.05),
            (!string.IsNullOrWhiteSpace(record.Description),  0.05),

            // Financial
            (record.DeclaredValue.HasValue,                0.05),
            (!string.IsNullOrWhiteSpace(record.Currency),  0.05),

            // Dates
            (record.ShipDate.HasValue, 0.05),
        };

        return Math.Round(Math.Min(1.0, checks
            .Where(c => c.present)
            .Sum(c => c.weight)), 4);
    }

    private static DateTime? ParseDate(string? v) =>
        DateTime.TryParse(v, out var d) ? d : null;

    private static DocumentType ParseDocumentType(string? v) =>
        Enum.TryParse<DocumentType>(v, true, out var r) ? r : DocumentType.Unknown;

    private static AiExtractionResponse Fail(string message) =>
        new(null, 0, string.Empty, false, message);
}

// ── Internal request/response DTOs ──────────────────────────────────────────

file sealed class OllamaGenerateRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("format")]
    public string Format { get; set; } = "json";
}

file sealed class OllamaGenerateResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("response")]
    public string? Response { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("done")]
    public bool Done { get; set; }
}

file sealed class MinimalFallbackDto
{
    [System.Text.Json.Serialization.JsonPropertyName("trackingNumber")]
    public string? TrackingNumber { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("shipperName")]
    public string? ShipperName { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("consigneeName")]
    public string? ConsigneeName { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("grossWeightKg")]
    public decimal? GrossWeightKg { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("declaredValue")]
    public decimal? DeclaredValue { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("currency")]
    public string? Currency { get; set; }
}
