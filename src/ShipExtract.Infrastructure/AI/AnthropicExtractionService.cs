using System.Text.Json;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.AI.Models;
using ShipExtract.Infrastructure.Settings;

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
        ExtractCoreAsync(rawText, hint, carrier, ct);

    /// <inheritdoc/>
    public async Task<AiExtractionResponse> ExtractAsync(
        string rawText,
        DocumentType hint = DocumentType.Unknown,
        CancellationToken ct = default) =>
        await ExtractCoreAsync(rawText, hint, CarrierType.Unknown, ct);

    private async Task<AiExtractionResponse> ExtractCoreAsync(
        string rawText,
        DocumentType hint,
        CarrierType carrier,
        CancellationToken ct)
    {
        try
        {
            var caller       = GetCaller();
            var systemPrompt = ExtractionPromptBuilder.BuildSystemPrompt();
            var userPrompt   = ExtractionPromptBuilder.BuildUserPrompt(rawText, hint, carrier);

            var rawResponse = await caller.SendAsync(
                systemPrompt, userPrompt, _settings.Model, _settings.MaxTokens, ct);

            var cleaned = StripMarkdownFences(rawResponse);

            return TryParseResponse(cleaned, rawResponse)
                ?? await RetryWithRepairAsync(cleaned, rawResponse, caller, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError("Anthropic API call failed", ex);
            return new AiExtractionResponse(null, 0, string.Empty, false, ex.Message);
        }
    }

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

    private AiExtractionResponse? TryParseResponse(string json, string rawResponse)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<ShipmentExtractionDto>(json, JsonOptions);
            return dto is null ? null : BuildSuccessResponse(dto, rawResponse);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<AiExtractionResponse> RetryWithRepairAsync(
        string badJson, string rawResponse, IAnthropicCaller caller, CancellationToken ct)
    {
        _logger.LogWarning("AI returned invalid JSON — attempting repair.");

        try
        {
            var repairPrompt =
                $"The following is not valid JSON. Fix it and return only the " +
                $"corrected JSON object with no explanation: {badJson}";

            var repaired = await caller.SendAsync(
                ExtractionPromptBuilder.BuildSystemPrompt(),
                repairPrompt,
                _settings.Model,
                _settings.MaxTokens,
                ct);

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

    private static AiExtractionResponse BuildSuccessResponse(ShipmentExtractionDto dto, string rawJson)
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

        return new AiExtractionResponse(record, record.ConfidenceScore, rawJson, true, null);
    }

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt : null;

    private static Domain.Enums.DocumentType ParseDocumentType(string? value) =>
        Enum.TryParse<Domain.Enums.DocumentType>(value, ignoreCase: true, out var result)
            ? result
            : Domain.Enums.DocumentType.Unknown;
}
