using Shouldly;
using Moq;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Exceptions;
using ShipExtract.Domain.Interfaces;
using ShipExtract.Infrastructure.Pdf;

namespace ShipExtract.Infrastructure.Tests.Pdf;

/// <summary>Tests that <see cref="PdfPigParser"/> validates files before attempting to parse.</summary>
public sealed class PdfValidationTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly Mock<ILoggingService> _logger = new();

    public PdfValidationTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private PdfPigParser CreateParser() => new(_logger.Object);

    [Fact]
    public async Task HasSelectableText_WhenFileNotFound_ThrowsPdfReadFailure()
    {
        var parser = CreateParser();
        var act = () => parser.HasSelectableTextAsync(@"C:\nonexistent_path_xyz\missing.pdf");

        var ex = await Should.ThrowAsync<ShipExtractException>(act);
        ex.ErrorCode.ShouldBe(ExtractionErrorCode.PdfReadFailure);
    }

    [Fact]
    public async Task HasSelectableText_WhenEmptyFile_ThrowsEmptyFile()
    {
        var path = Path.Combine(_tempDir, "empty.pdf");
        await File.WriteAllBytesAsync(path, []);

        var parser = CreateParser();
        var act = () => parser.HasSelectableTextAsync(path);

        var ex = await Should.ThrowAsync<ShipExtractException>(act);
        ex.ErrorCode.ShouldBe(ExtractionErrorCode.EmptyFile);
    }

    [Fact]
    public async Task HasSelectableText_WhenMissingMagicBytes_ThrowsCorruptFile()
    {
        var path = Path.Combine(_tempDir, "notpdf.pdf");
        await File.WriteAllBytesAsync(path, [0x00, 0x01, 0x02, 0x03, 0x04]);

        var parser = CreateParser();
        var act = () => parser.HasSelectableTextAsync(path);

        var ex = await Should.ThrowAsync<ShipExtractException>(act);
        ex.ErrorCode.ShouldBe(ExtractionErrorCode.CorruptFile);
    }

    [Fact]
    public async Task ExtractText_WhenFileNotFound_ThrowsPdfReadFailure()
    {
        var parser = CreateParser();
        var act = () => parser.ExtractTextAsync(@"C:\nonexistent_path_xyz\missing.pdf");

        var ex = await Should.ThrowAsync<ShipExtractException>(act);
        ex.ErrorCode.ShouldBe(ExtractionErrorCode.PdfReadFailure);
    }
}
