using System.Text.Json;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.AI.Models;
using ShipExtract.Infrastructure.Settings;
using System.Collections.Generic;

namespace ShipExtract.Infrastructure.AI;

/// <summary>
/// Implements <see cref="IAiExtractionService"/> using the Anthropic Claude API
/// to extract structured shipment data from raw document text.
/// </summary>
public sealed class AnthropicExtractionService : IAiExtractionService
{
    private readonly IAnthropicCaller _caller;
    private readonly AnthropicSettings _settings;
    private readonly ILoggingService _logger;
    private readonly ICredentialService _credentialService;

    private string? _cachedApiKey;
    private IAnthropicCaller? _cachedCaller;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initialises a new instance of <see cref="AnthropicExtractionService"/>.</summary>
    public AnthropicExtractionService(
        IAnthropicCaller caller,
        AnthropicSettings settings,
        ILoggingService logger,
        ICredentialService credentialService)
    {
        _caller            = caller;
        _settings          = settings;
        _logger            = logger;
        _credentialService = credentialService;
    }

    private IAnthropicCaller GetCaller()
    {
        var apiKey = _credentialService.GetApiKey() ?? _settings.ApiKey;
        if (apiKey == _cachedApiKey && _cachedCaller is not null)
            return _cachedCaller;

        _cachedApiKey = apiKey;
        var newClient = new Anthropic.SDK.AnthropicClient(
            new Anthropic.SDK.APIAuthentication(apiKey));
        _cachedCaller = new AnthropicCallerAdapter(newClient);
        return _cachedCaller;
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
        try
        {
            var systemPrompt = ExtractionPromptBuilder.BuildSystemPrompt();
            var userPrompt   = ExtractionPromptBuilder.BuildUserPrompt(rawText, hint, carrier, customFields);

            var rawResponse = await ExecuteWithRetryAsync(systemPrompt, userPrompt, ct);

            var cleaned = StripMarkdownFences(rawResponse);

            return TryParseResponse(cleaned, rawResponse, customFields)
                ?? await RetryWithRepairAsync(cleaned, rawResponse, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError("Anthropic API call failed", ex);
            return new AiExtractionResponse(null, 0, string.Empty, false, ex.Message);
        }
    }

    /// <summary>
    /// Calls the Anthropic API with up to 3 attempts, waiting 15 s then 30 s on 429/rate-limit errors.
    /// </summary>
    private async Task<string> ExecuteWithRetryAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        int[] waitSeconds = [0, 15, 30];

        for (int attempt = 0; attempt < waitSeconds.Length; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogWarning(
                    "Anthropic rate-limited — waiting {Secs}s before attempt {Attempt}.",
                    waitSeconds[attempt], attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(waitSeconds[attempt]), ct);
            }

            try
            {
                var caller = GetCaller();
                return await caller.SendAsync(
                    systemPrompt, userPrompt, _settings.Model, _settings.MaxTokens, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested && IsRateLimitError(ex.Message))
            {
                if (attempt == waitSeconds.Length - 1)
                    throw; // rethrow on final attempt
            }
        }

        throw new InvalidOperationException("Unreachable: retry loop exhausted without result.");
    }

    private static bool IsRateLimitError(string message) =>
        message.Contains("429",        StringComparison.OrdinalIgnoreCase) ||
        message.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("overloaded", StringComparison.OrdinalIgnoreCase);

    private static string StripMarkdownFences(string text)
    {
        var s = text.Trim();
        if (s.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            s = s["```json".Length..];
        else if (s.StartsWith("```"))
            s = s[3..];
        if (s.EndsWith("```"))
            s = s[..^3];
        return s.Trim();
    }

    private AiExtractionResponse? TryParseResponse(string json, string rawResponse, List<CustomField>? customFields = null)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<ShipmentExtractionDto>(json, JsonOptions);
            return dto is null ? null : BuildSuccessResponse(dto, rawResponse, customFields);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<AiExtractionResponse> RetryWithRepairAsync(
        string badJson, string rawResponse, CancellationToken ct)
    {
        _logger.LogWarning("AI returned invalid JSON — attempting repair.");

        try
        {
            var repairPrompt =
                $"The following is not valid JSON. Fix it and return only the " +
                $"corrected JSON object with no explanation: {badJson}";

            var repaired = await ExecuteWithRetryAsync(
                ExtractionPromptBuilder.BuildSystemPrompt(), repairPrompt, ct);

            repaired = StripMarkdownFences(repaired);
            return TryParseResponse(repaired, rawResponse)
                   ?? new AiExtractionResponse(null, 0, rawResponse, false, "JSON repair failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError("JSON repair retry failed", ex);
            return new AiExtractionResponse(null, 0, rawResponse, false, ex.Message);
        }
    }

    private static AiExtractionResponse BuildSuccessResponse(
        ShipmentExtractionDto dto, string rawJson, List<CustomField>? customFields = null)
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
            ConfidenceScore       = dto.ConfidenceScore ?? 0.5
        };

        record.CustomFields = MapCustomFields(dto.CustomFields, customFields);

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

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt : null;

    private static Domain.Enums.DocumentType ParseDocumentType(string? value) =>
        Enum.TryParse<Domain.Enums.DocumentType>(value, ignoreCase: true, out var result)
            ? result
            : Domain.Enums.DocumentType.Unknown;
}
