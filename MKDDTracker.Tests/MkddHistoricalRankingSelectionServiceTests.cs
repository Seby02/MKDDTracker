using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalRankingSelectionServiceTests
{
    [Fact]
    public async Task GetDateRangeAsync_FiltersByCourseTypeAndFrequencyAndCalculatesBoundsInMemory()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "historical-ranking-selection.db");

        try
        {
            await using var db = DatabaseTestSupport.CreateContext(databasePath);
            await db.Database.EnsureCreatedAsync();
            var course = new MkddCourse { Name = "Luigi Circuit" };
            var alias = new MkddHistoricalPlayerAlias
            {
                SourceSystem = "YahooGroups",
                ExternalName = "Selection Player",
                NormalizedExternalName = "selection-player",
                Confidence = 0,
                ResolutionStatus = "Unresolved"
            };

            db.HistoricalPerformances.AddRange(
                CreatePerformance("60-first", course, alias, "ThreeLap", 60,
                    new DateTimeOffset(2004, 1, 1, 12, 0, 0, TimeSpan.Zero)),
                CreatePerformance("60-last", course, alias, "ThreeLap", 60,
                    new DateTimeOffset(2004, 1, 3, 12, 0, 0, TimeSpan.Zero)),
                CreatePerformance("50-only", course, alias, "ThreeLap", 50,
                    new DateTimeOffset(2004, 2, 1, 12, 0, 0, TimeSpan.Zero)),
                CreatePerformance("unknown-only", course, alias, "ThreeLap", null,
                    new DateTimeOffset(2004, 3, 1, 12, 0, 0, TimeSpan.Zero)),
                CreatePerformance("other-type", course, alias, "Flap", 60,
                    new DateTimeOffset(2004, 4, 1, 12, 0, 0, TimeSpan.Zero)));
            await db.SaveChangesAsync();

            var service = new MkddHistoricalRankingSelectionService(
                new TestDbContextFactory(databasePath));

            HistoricalPerformanceDateRange hz60 = await service.GetDateRangeAsync(
                "Luigi Circuit", "ThreeLap", HistoricalFrequencyFilter.Hz60);
            HistoricalPerformanceDateRange hz50 = await service.GetDateRangeAsync(
                "Luigi Circuit", "ThreeLap", HistoricalFrequencyFilter.Hz50);
            HistoricalPerformanceDateRange unknown = await service.GetDateRangeAsync(
                "Luigi Circuit", "ThreeLap", HistoricalFrequencyFilter.Unknown);
            HistoricalPerformanceDateRange noFlapUnknown = await service.GetDateRangeAsync(
                "Luigi Circuit", "Flap", HistoricalFrequencyFilter.Unknown);

            Assert.Equal(2, hz60.Count);
            Assert.Equal(new DateTimeOffset(2004, 1, 1, 12, 0, 0, TimeSpan.Zero), hz60.FirstSeenAt);
            Assert.Equal(new DateTimeOffset(2004, 1, 3, 12, 0, 0, TimeSpan.Zero), hz60.LastSeenAt);
            Assert.Equal(1, hz50.Count);
            Assert.Equal(1, unknown.Count);
            Assert.Equal(0, noFlapUnknown.Count);
            Assert.Null(noFlapUnknown.FirstSeenAt);
            Assert.Null(noFlapUnknown.LastSeenAt);
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    private static MkddHistoricalPerformance CreatePerformance(
        string key,
        MkddCourse course,
        MkddHistoricalPlayerAlias alias,
        string timeType,
        int? frequency,
        DateTimeOffset firstSeenAt) => new()
    {
        ExternalKey = key,
        Course = course,
        HistoricalPlayerAlias = alias,
        TimeType = timeType,
        Frequency = frequency,
        Time = TimeSpan.FromSeconds(80),
        FirstSeenAt = firstSeenAt,
        DateSource = "YahooFirstSeenDate",
        SequenceNumber = 1,
        OccurrenceCount = 1,
        SourceSystem = "YahooGroups",
        ImportStatus = "Approved"
    };

    private sealed class TestDbContextFactory(string databasePath)
        : IDbContextFactory<MkddDbContext>
    {
        public MkddDbContext CreateDbContext() =>
            DatabaseTestSupport.CreateContext(databasePath);

        public Task<MkddDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
