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
    private readonly ILoggingService _logger;

    /// <summary>Initialises a new instance of <see cref="ExtractionPipeline"/>.</summary>
    public ExtractionPipeline(
        IPdfParser pdfParser,
        IOcrService ocrService,
        IAiExtractionService aiService,
        ILoggingService logger)
    {
        _pdfParser = pdfParser;
        _ocrService = ocrService;
        _aiService = aiService;
        _logger = logger;
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
            var hasSelectableText = await _pdfParser.HasSelectableTextAsync(filePath, ct);

            if (hasSelectableText)
            {
                _logger.LogDebug("PDF has selectable text, extracting directly: {FilePath}", filePath);
                text = await _pdfParser.ExtractTextAsync(filePath, ct);
            }
            else
            {
                _logger.LogDebug("PDF lacks selectable text, falling back to OCR: {FilePath}", filePath);
                var pageImages = await _pdfParser.RenderPagesToImagesAsync(filePath, 200, ct);
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

            // Step 4 — AI extraction
            var aiResponse = await _aiService.ExtractAsync(text, DocumentType.Unknown, ct);
            result.RawAiResponse = aiResponse.RawJson;

            // Steps 5 & 6 — handle AI response
            if (aiResponse.Success && aiResponse.Record is not null)
            {
                var record = aiResponse.Record;
                record.SourceFileName = Path.GetFileName(filePath);
                record.ConfidenceScore = aiResponse.ConfidenceScore;
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
                    _logger.LogWarning("Extraction for {FilePath} had validation issues: {Errors}",
                        filePath, string.Join("; ", validation.Errors));
                }
                else
                {
                    result.Status = ProcessingStatus.Succeeded;
                    _logger.LogInformation("Extraction succeeded for {FilePath} (confidence {Score:P0})",
                        filePath, aiResponse.ConfidenceScore);
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
                _logger.LogError("AI extraction failed for {FilePath}: {Error}",
                    null, filePath, aiResponse.ErrorMessage);
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
}
