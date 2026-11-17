using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Services.SnapshotService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddSnapshotHistoryMigrationServiceTests
{
    [Fact]
    public async Task Migration_ImportsOnlyStrictPrImprovementsAndIsIdempotent()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "snapshot-history.db");

        try
        {
            await using var db = DatabaseTestSupport.CreateContext(databasePath);
            await db.Database.EnsureCreatedAsync();

            var player = new MkddPlayer
            {
                SitePlayerId = 101,
                Name = "Modern Player",
                Country = "FR"
            };
            var firstCourse = new MkddCourse { Name = "Luigi Circuit" };
            var secondCourse = new MkddCourse { Name = "Peach Beach" };

            AddSnapshot(db, player, firstCourse, new DateOnly(2026, 10, 1), 83_000, new DateOnly(2026, 9, 30));
            AddSnapshot(db, player, firstCourse, new DateOnly(2026, 10, 2), 83_000);
            AddSnapshot(db, player, firstCourse, new DateOnly(2026, 10, 3), 84_000);
            AddSnapshot(db, player, firstCourse, new DateOnly(2026, 10, 4), 82_500, new DateOnly(2026, 10, 5));
            AddSnapshot(db, player, secondCourse, new DateOnly(2026, 10, 1), 90_000);
            await db.SaveChangesAsync();

            var service = new MkddSnapshotHistoryMigrationService(db);
            MkddSnapshotHistoryMigrationResult dryRun = await service.AnalyzeAsync();

            Assert.Equal(5, dryRun.SnapshotPerformancesAnalyzed);
            Assert.Equal(2, dryRun.PlayerCourseSeriesAnalyzed);
            Assert.Equal(3, dryRun.PrEventsToCreate);
            Assert.Equal(1, dryRun.IdenticalSnapshotsIgnored);
            Assert.Equal(1, dryRun.SlowerTimesIgnored);
            Assert.Equal(1, dryRun.ModernAliasesToCreate);
            Assert.Equal(new DateOnly(2026, 9, 30), dryRun.EarliestRecordDate);
            Assert.Equal(new DateOnly(2026, 10, 5), dryRun.LatestRecordDate);
            Assert.Equal(new DateOnly(2026, 10, 1), dryRun.EarliestRankingDate);
            Assert.Equal(new DateOnly(2026, 10, 4), dryRun.LatestRankingDate);
            Assert.Equal(0, await db.HistoricalPerformances.CountAsync());

            MkddSnapshotHistoryMigrationResult imported = await service.ImportAsync();

            Assert.Equal(3, imported.PrEventsImported);
            Assert.Equal(new DateOnly(2026, 9, 30), imported.EarliestRecordDate);
            Assert.Equal(new DateOnly(2026, 10, 5), imported.LatestRecordDate);
            Assert.Equal(new DateOnly(2026, 10, 1), imported.EarliestRankingDate);
            Assert.Equal(new DateOnly(2026, 10, 4), imported.LatestRankingDate);
            var history = await db.HistoricalPerformances
                .OrderBy(item => item.CourseId)
                .ThenBy(item => item.SequenceNumber)
                .ToListAsync();
            Assert.Equal(3, history.Count);

            var firstSeries = history
                .Where(item => item.CourseId == firstCourse.Id)
                .OrderBy(item => item.SequenceNumber)
                .ToList();
            Assert.Equal(2, firstSeries.Count);
            Assert.Equal(TimeSpan.FromMilliseconds(83_000), firstSeries[0].Time);
            Assert.Null(firstSeries[0].PreviousTime);
            Assert.Equal(2, firstSeries[0].OccurrenceCount);
            Assert.Equal("ThreeLap", firstSeries[0].TimeType);
            Assert.Null(firstSeries[0].Frequency);
            Assert.Equal(TimeSpan.FromMilliseconds(82_500), firstSeries[1].Time);
            Assert.Equal(TimeSpan.FromMilliseconds(83_000), firstSeries[1].PreviousTime);
            Assert.Equal(TimeSpan.FromMilliseconds(500), firstSeries[1].Improvement);
            Assert.Equal(2, firstSeries[1].SequenceNumber);
            Assert.Equal(
                new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero),
                firstSeries[1].FirstSeenAt);

            var alias = Assert.Single(await db.HistoricalPlayerAliases.ToListAsync());
            Assert.Equal("MKDDTracker", alias.SourceSystem);
            Assert.Equal("Modern Player", alias.ExternalName);
            Assert.Equal("modern player", alias.NormalizedExternalName);
            Assert.Equal(player.Id, alias.PlayerId);
            Assert.Equal(100, alias.Confidence);
            Assert.Equal("Matched", alias.ResolutionStatus);

            MkddSnapshotHistoryMigrationResult secondDryRun = await service.AnalyzeAsync();
            Assert.Equal(0, secondDryRun.PrEventsToCreate);
            Assert.Equal(3, secondDryRun.ExistingPrEvents);

            MkddSnapshotHistoryMigrationResult secondImport = await service.ImportAsync();
            Assert.Equal(0, secondImport.PrEventsImported);
            Assert.Equal(3, await db.HistoricalPerformances.CountAsync());
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    private static void AddSnapshot(
        MKDDTracker.Scraper.Data.Database.MkddDbContext db,
        MkddPlayer player,
        MkddCourse course,
        DateOnly date,
        long milliseconds,
        DateOnly? recordDate = null)
    {
        var snapshot = new MkddSnapshot
        {
            Course = course,
            CapturedAt = date.ToDateTime(new TimeOnly(12, 0)),
            RankingDate = date
        };
        snapshot.Performances.Add(new MkddPerformanceEntity
        {
            Player = player,
            Rank = 1,
            Time = TimeSpan.FromMilliseconds(milliseconds),
            RecordDate = recordDate ?? date
        });
        db.Snapshots.Add(snapshot);
    }
}
