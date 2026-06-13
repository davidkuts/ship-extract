using System.Text.Json;
using Shouldly;
using ShipExtract.Domain.Enums;
using ShipExtract.Domain.Models;
using ShipExtract.Infrastructure.History;

namespace ShipExtract.Infrastructure.Tests.History;

public sealed class BatchHistoryServiceTests : IDisposable
{
    // Each test gets its own temp directory to avoid cross-test interference.
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private BatchHistoryService Sut() => new(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // ── 1 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Save_PersistsIndexFileAndDetailFile()
    {
        var sut = Sut();
        var job = MakeJob(successCount: 2, failureCount: 0);

        await sut.SaveAsync(job);

        var indexPath  = Path.Combine(_dir, "index.json");
        var detailPath = Path.Combine(_dir, $"{job.Id:N}.json");

        File.Exists(indexPath).ShouldBeTrue("index.json must be created");
        File.Exists(detailPath).ShouldBeTrue("per-batch detail file must be created");
    }

    // ── 2 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetAll_ReturnsAllSavedEntries_NewestFirst()
    {
        var sut = Sut();
        var job1 = MakeJob(successCount: 1, failureCount: 0);
        var job2 = MakeJob(successCount: 3, failureCount: 1);

        await sut.SaveAsync(job1);
        await sut.SaveAsync(job2);

        var entries = await sut.GetAllAsync();

        entries.Count.ShouldBe(2);
        entries[0].BatchId.ShouldBe(job2.Id, "newest batch should appear first");
        entries[1].BatchId.ShouldBe(job1.Id);
    }

    // ── 3 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task RoundTrip_PreservesAllSummaryFields()
    {
        var sut = Sut();
        var job = MakeJob(successCount: 4, failureCount: 2);

        await sut.SaveAsync(job);

        var all   = await sut.GetAllAsync();
        var entry = all.Single();

        entry.BatchId.ShouldBe(job.Id);
        entry.TotalFiles.ShouldBe(job.TotalFiles);
        entry.SuccessCount.ShouldBe(job.SuccessCount);
        entry.FailureCount.ShouldBe(job.FailureCount);
        entry.TotalDurationSeconds.ShouldBeGreaterThanOrEqualTo(0);
    }

    // ── 4 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Save_TrimsToThirtyEntries_RemovingOldest()
    {
        var sut  = Sut();

        // Save 32 batches
        Guid firstId  = Guid.Empty;
        Guid secondId = Guid.Empty;
        for (int i = 0; i < 32; i++)
        {
            var job = MakeJob(successCount: 1, failureCount: 0);
            if (i == 0)  firstId  = job.Id;
            if (i == 1)  secondId = job.Id;
            await sut.SaveAsync(job);
        }

        var entries = await sut.GetAllAsync();

        entries.Count.ShouldBe(30, "history must be capped at 30");
        entries.Select(e => e.BatchId).ShouldNotContain(firstId);
        entries.Select(e => e.BatchId).ShouldNotContain(secondId);

        // Detail files for trimmed entries must also be deleted
        var firstDetail  = Path.Combine(_dir, $"{firstId:N}.json");
        var secondDetail = Path.Combine(_dir, $"{secondId:N}.json");
        File.Exists(firstDetail).ShouldBeFalse("trimmed detail file must be deleted");
        File.Exists(secondDetail).ShouldBeFalse("trimmed detail file must be deleted");
    }

    // ── 5 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Delete_RemovesEntryAndDetailFile()
    {
        var sut = Sut();
        var job = MakeJob(successCount: 1, failureCount: 0);
        await sut.SaveAsync(job);

        await sut.DeleteAsync(job.Id);

        var entries    = await sut.GetAllAsync();
        var detailPath = Path.Combine(_dir, $"{job.Id:N}.json");

        entries.ShouldBeEmpty();
        File.Exists(detailPath).ShouldBeFalse();
    }

    // ── 6 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ClearAll_RemovesAllEntriesAndDetailFiles()
    {
        var sut = Sut();
        var job1 = MakeJob(successCount: 1, failureCount: 0);
        var job2 = MakeJob(successCount: 2, failureCount: 0);
        await sut.SaveAsync(job1);
        await sut.SaveAsync(job2);

        await sut.ClearAllAsync();

        var entries = await sut.GetAllAsync();
        entries.ShouldBeEmpty();
        File.Exists(Path.Combine(_dir, $"{job1.Id:N}.json")).ShouldBeFalse();
        File.Exists(Path.Combine(_dir, $"{job2.Id:N}.json")).ShouldBeFalse();
    }

    // ── 7 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PrimaryCarrier_ReturnsTheMostFrequentCarrier()
    {
        var results = new List<ProcessingResult>
        {
            MakeResult(CarrierType.DHL),
            MakeResult(CarrierType.DHL),
            MakeResult(CarrierType.FedEx)
        };

        var primary = BatchHistoryService.ComputePrimaryCarrier(results);

        primary.ShouldBe(CarrierType.DHL);
    }

    // ── 8 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task PrimaryCarrier_ReturnsUnknown_WhenNoResultsHaveCarrier()
    {
        var results = new List<ProcessingResult>
        {
            MakeResult(CarrierType.Unknown),
            MakeResult(CarrierType.Unknown)
        };

        var primary = BatchHistoryService.ComputePrimaryCarrier(results);

        primary.ShouldBe(CarrierType.Unknown);
    }

    // ── 9 ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CorruptIndex_ReturnsEmptyList()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(Path.Combine(_dir, "index.json"), "NOT_VALID_JSON");

        var sut     = Sut();
        var entries = await sut.GetAllAsync();

        entries.ShouldBeEmpty("corrupt index must be treated as empty");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static BatchJob MakeJob(int successCount, int failureCount)
    {
        var filePaths = Enumerable.Range(0, successCount + failureCount)
            .Select(i => $"C:\\test\\file{i}.pdf")
            .ToList();

        var job = new BatchJob
        {
            FilePaths    = filePaths,
            CreatedAt    = DateTimeOffset.UtcNow.AddSeconds(-10),
            Status       = BatchStatus.Completed,
            SuccessCount = successCount,
            FailureCount = failureCount
        };
        job.ProcessedFiles  = successCount + failureCount;
        job.CompletedAt     = DateTimeOffset.UtcNow;

        foreach (var path in filePaths)
            job.Results.Add(new ProcessingResult
            {
                SourceFilePath = path,
                Status         = ProcessingStatus.Succeeded
            });

        return job;
    }

    private static ProcessingResult MakeResult(CarrierType carrier) =>
        new()
        {
            SourceFilePath  = "C:\\test\\file.pdf",
            Status          = ProcessingStatus.Succeeded,
            DetectedCarrier = carrier
        };
}
