using System.Diagnostics;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Domain.Models;
using ShipExtract.Domain.Validators;


namespace ShipExtract.Application.Pipelines;

/// <summary>
/// Orchestrates the full extraction pipeline for a single PDF document:
/// text extraction (native or OCR), AI-based field extraction, and validation.
/// </summary>
public sealed class ExtractionPipeline
{
    private readonly IPdfParser _pdfParser;
    private readonly IOcrService _ocrService;
    private readonly IAiExtractionService _aiService;
    private readonly ITextPreProcessingPipeline? _preProcessingPipeline;
    private readonly ICarrierDetector? _carrierDetector;
    private readonly ILoggingService _logger;

    /// <summary>Initialises a new instance of <see cref="ExtractionPipeline"/>.</summary>
    public ExtractionPipeline(
        IPdfParser pdfParser,
        IOcrService ocrService,
        IAiExtractionService aiService,
        ILoggingService logger,
        ITextPreProcessingPipeline? preProcessingPipeline = null,
        ICarrierDetector? carrierDetector = null)
    {
        _pdfParser             = pdfParser;
        _ocrService            = ocrService;
        _aiService             = aiService;
        _logger                = logger;
        _preProcessingPipeline = preProcessingPipeline;
        _carrierDetector       = carrierDetector;
    }

    /// <summary>
    /// Processes a single PDF file through the extraction pipeline and returns the result.
    /// </summary>
    /// <param name="filePath">Absolute path to the PDF file to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ProcessingResult"/> describing the outcome of the extraction.</returns>
    public async Task<ProcessingResult> ProcessAsync(string filePath, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ProcessingResult { SourceFilePath = filePath };

        try
        {
            _logger.LogInformation("Starting extraction for file: {FilePath}", filePath);

            // Step 1 — validate file
            if (!File.Exists(filePath) ||
                !string.Equals(Path.GetExtension(filePath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                result.Status = ProcessingStatus.Failed;
                result.Errors.Add(new ExtractionError
                {
                    Code = ExtractionErrorCode.UnsupportedFormat,
                    Message = $"File not found or not a PDF: {filePath}"
                });
                return result;
            }

            // Step 2 — text extraction
            string text;
            bool hasSelectableText;

            try
            {
                hasSelectableText = await _pdfParser.HasSelectableTextAsync(filePath, ct);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("PDF could not be opened"))
            {
                result.Status = ProcessingStatus.Failed;
                result.Errors.Add(new ExtractionError
                {
                    Code = ExtractionErrorCode.UnsupportedFormat,
                    Message = ex.Message,
                    Exception = ex
                });
                return result;
            }

            if (hasSelectableText)
            {
                _logger.LogDebug("PDF has selectable text, extracting directly: {FilePath}", filePath);
                try
                {
                    text = await _pdfParser.ExtractTextAsync(filePath, ct);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("PDF could not be opened"))
                {
                    result.Status = ProcessingStatus.Failed;
                    result.Errors.Add(new ExtractionError
                    {
                        Code = ExtractionErrorCode.UnsupportedFormat,
                        Message = ex.Message,
                        Exception = ex
                    });
                    return result;
                }
            }
            else
            {
                _logger.LogDebug("PDF lacks selectable text, falling back to OCR: {FilePath}", filePath);
                IReadOnlyList<byte[]> pageImages;
                try
                {
                    pageImages = await _pdfParser.RenderPagesToImagesAsync(filePath, 200, ct);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("PDF could not be opened"))
                {
                    result.Status = ProcessingStatus.Failed;
                    result.Errors.Add(new ExtractionError
                    {
                        Code = ExtractionErrorCode.UnsupportedFormat,
                        Message = ex.Message,
                        Exception = ex
                    });
                    return result;
                }
                text = await _ocrService.RecognizeTextFromPagesAsync(pageImages, ct);
                result.UsedOcrFallback = true;
            }

            // Step 3 — guard against empty text
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Status = ProcessingStatus.Failed;
                result.Errors.Add(new ExtractionError
                {
                    Code = ExtractionErrorCode.UnsupportedFormat,
                    Message = "No text could be extracted from the document."
                });
                return result;
            }

            // Store ORIGINAL raw text for debugging (truncated to 50 k chars).
            result.ExtractedRawText = text.Length > 50000
                ? text[..50000] + "\n\n[Truncated — showing first 50,000 characters]"
                : text;

            // Step 2.5 — Pre-process: clean the text before sending to AI (when available).
            string textForAi;
            if (_preProcessingPipeline is not null)
            {
                var (cleaned, preProcessingReport) = _preProcessingPipeline.Process(text);
                result.PreProcessingReport = preProcessingReport;
                textForAi = cleaned;
            }
            else
            {
                textForAi = text;
            }

            // Step 2.6 — Carrier detection
            var detectedCarrier = _carrierDetector?.Detect(textForAi) ?? CarrierType.Unknown;
            result.DetectedCarrier = detectedCarrier;
            _logger.LogInformation("Carrier detected: {Carrier} for {File}",
                detectedCarrier, Path.GetFileName(filePath));

            // Step 4 — AI extraction (with one transient retry)
            var aiResponse = await CallAiWithRetryAsync(textForAi, detectedCarrier, ct);
            result.RawAiResponse          = aiResponse.RawJson;
            result.UsedFallbackExtraction = aiResponse.UsedFallbackExtraction;

            // Steps 5 & 6 — handle AI response
            if (aiResponse.Success && aiResponse.Record is not null)
            {
                var record = aiResponse.Record;
                record.SourceFileName   = Path.GetFileName(filePath);
                record.ConfidenceScore  = aiResponse.ConfidenceScore;
                record.DetectedCarrier  = detectedCarrier;

                // Fall back to detected carrier name if the AI left CarrierName empty
                if (string.IsNullOrWhiteSpace(record.CarrierName) && detectedCarrier != CarrierType.Unknown)
                    record.CarrierName = detectedCarrier.ToString();

                result.Record = record;

                var validation = ShipmentRecordValidator.Validate(record);
                if (!validation.IsValid)
                {
                    result.Status = ProcessingStatus.PartialSuccess;
                    foreach (var error in validation.Errors)
                    {
                        result.Errors.Add(new ExtractionError
                        {
                            Code = ExtractionErrorCode.ValidationFailure,
                            Message = error
                        });
                    }
                    _logger.LogWarning("Extraction for {FilePath} had validation errors: {Errors}",
                        filePath, string.Join("; ", validation.Errors));
                }
                else
                {
                    result.Status = ProcessingStatus.Succeeded;
                    _logger.LogInformation("Extraction succeeded for {FilePath} (confidence {Score:P0})",
                        filePath, aiResponse.ConfidenceScore);
                    if (validation.HasWarnings)
                    {
                        _logger.LogDebug("Extraction warnings for {FilePath}: {Warnings}",
                            filePath, string.Join("; ", validation.Warnings));
                    }
                }
            }
            else
            {
                result.Status = ProcessingStatus.Failed;
                result.Errors.Add(new ExtractionError
                {
                    Code = ExtractionErrorCode.AiCallFailure,
                    Message = aiResponse.ErrorMessage ?? "AI extraction returned no data."
                });
                _logger.LogError($"AI extraction failed for {filePath}: {aiResponse.ErrorMessage}");
            }
        }
        catch (OperationCanceledException)
        {
            result.Status = ProcessingStatus.Failed;
            result.Errors.Add(new ExtractionError
            {
                Code = ExtractionErrorCode.AiCallFailure,
                Message = "Processing was cancelled."
            });
            _logger.LogWarning("Processing cancelled for {FilePath}", filePath);
            throw;
        }
        catch (Exception ex)
        {
            result.Status = ProcessingStatus.Failed;
            result.Errors.Add(new ExtractionError
            {
                Code = ExtractionErrorCode.AiCallFailure,
                Message = $"Unexpected error: {ex.Message}",
                Exception = ex
            });
            _logger.LogError("Unexpected error processing {FilePath}", ex, filePath);
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingDuration = stopwatch.Elapsed;
        }

        return result;
    }

    private async Task<Domain.Interfaces.AiExtractionResponse> CallAiWithRetryAsync(
        string text, CarrierType carrier, CancellationToken ct)
    {
        try
        {
            // Prefer carrier-aware overload; fall back to base overload when the
            // carrier-aware version is not set up (e.g. existing unit-test mocks).
            var response = await _aiService.ExtractAsync(text, Domain.Enums.DocumentType.Unknown, carrier, ct);
            return response ?? await _aiService.ExtractAsync(text, Domain.Enums.DocumentType.Unknown, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && IsTransientAiError(ex.Message))
        {
            _logger.LogWarning("Transient AI error — retrying once after 5 s. Reason: {Reason}", ex.Message);
            await Task.Delay(5000, ct);
            var response = await _aiService.ExtractAsync(text, Domain.Enums.DocumentType.Unknown, carrier, ct);
            return response ?? await _aiService.ExtractAsync(text, Domain.Enums.DocumentType.Unknown, ct);
        }
    }

    private static bool IsTransientAiError(string message) =>
        message.Contains("rate",       StringComparison.OrdinalIgnoreCase) ||
        message.Contains("529",        StringComparison.OrdinalIgnoreCase) ||
        message.Contains("overloaded", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("timeout",    StringComparison.OrdinalIgnoreCase);
}
