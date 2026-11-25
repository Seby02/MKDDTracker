using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Services.SnapshotService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddSnapshotRetirementCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_NoLegacyData_IsReady()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "snapshot-retirement-empty.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            var service =
                new MkddSnapshotRetirementCheckService(db);

            MkddSnapshotRetirementCheckResult result =
                await service.CheckAsync();

            Assert.Equal(
                0,
                result.SnapshotCount);

            Assert.Equal(
                0,
                result.LegacyPerformanceCount);

            Assert.Equal(
                0,
                result.SnapshotPerformancesAnalyzed);

            Assert.Equal(
                0,
                result.PrEventsToCreate);

            Assert.Equal(
                0,
                result.ModernAliasesToCreate);

            Assert.True(
                result.IsReadyForRetirement);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
    public async Task CheckAsync_UnmigratedLegacyData_IsNotReady()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "snapshot-retirement-not-ready.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            var player =
                new MkddPlayer
                {
                    SitePlayerId = 101,
                    Name = "Modern Player",
                    Country = "FR"
                };

            var course =
                new MkddCourse
                {
                    Name = "Luigi Circuit"
                };

            AddSnapshot(
                db,
                player,
                course,
                new DateOnly(2026, 10, 1),
                83_000);

            await db.SaveChangesAsync();

            var service =
                new MkddSnapshotRetirementCheckService(db);

            MkddSnapshotRetirementCheckResult result =
                await service.CheckAsync();

            Assert.Equal(
                1,
                result.SnapshotCount);

            Assert.Equal(
                1,
                result.LegacyPerformanceCount);

            Assert.Equal(
                1,
                result.SnapshotPerformancesAnalyzed);

            Assert.True(
                result.PrEventsToCreate > 0);

            Assert.False(
                result.IsReadyForRetirement);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
    public async Task CheckAsync_MigratedLegacyData_IsReady()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "snapshot-retirement-ready.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            var player =
                new MkddPlayer
                {
                    SitePlayerId = 101,
                    Name = "Modern Player",
                    Country = "FR"
                };

            var course =
                new MkddCourse
                {
                    Name = "Luigi Circuit"
                };

            AddSnapshot(
                db,
                player,
                course,
                new DateOnly(2026, 10, 1),
                83_000);

            AddSnapshot(
                db,
                player,
                course,
                new DateOnly(2026, 10, 2),
                82_500);

            await db.SaveChangesAsync();

            var migrationService =
                new MkddSnapshotHistoryMigrationService(db);

            MkddSnapshotHistoryMigrationResult migration =
                await migrationService.ImportAsync();

            Assert.True(
                migration.PrEventsImported > 0);

            var service =
                new MkddSnapshotRetirementCheckService(db);

            MkddSnapshotRetirementCheckResult result =
                await service.CheckAsync();

            Assert.Equal(
                2,
                result.SnapshotCount);

            Assert.Equal(
                2,
                result.LegacyPerformanceCount);

            Assert.Equal(
                0,
                result.PrEventsToCreate);

            Assert.Equal(
                0,
                result.ModernAliasesToCreate);

            Assert.True(
                result.ExistingPrEvents > 0);

            Assert.True(
                result.IsReadyForRetirement);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
    public async Task CheckAsync_DoesNotModifyDatabase()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "snapshot-retirement-read-only.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            var player =
                new MkddPlayer
                {
                    SitePlayerId = 101,
                    Name = "Modern Player",
                    Country = "FR"
                };

            var course =
                new MkddCourse
                {
                    Name = "Luigi Circuit"
                };

            AddSnapshot(
                db,
                player,
                course,
                new DateOnly(2026, 10, 1),
                83_000);

            await db.SaveChangesAsync();

            int snapshotsBefore =
                await db.Snapshots.CountAsync();

            int performancesBefore =
                await db.Performances.CountAsync();

            int aliasesBefore =
                await db.HistoricalPlayerAliases.CountAsync();

            int historyBefore =
                await db.HistoricalPerformances.CountAsync();

            var service =
                new MkddSnapshotRetirementCheckService(db);

            await service.CheckAsync();

            Assert.Equal(
                snapshotsBefore,
                await db.Snapshots.CountAsync());

            Assert.Equal(
                performancesBefore,
                await db.Performances.CountAsync());

            Assert.Equal(
                aliasesBefore,
                await db.HistoricalPlayerAliases.CountAsync());

            Assert.Equal(
                historyBefore,
                await db.HistoricalPerformances.CountAsync());
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    private static void AddSnapshot(
        MkddDbContext db,
        MkddPlayer player,
        MkddCourse course,
        DateOnly date,
        long milliseconds)
    {
        var snapshot =
            new MkddSnapshot
            {
                Course = course,

                CapturedAt =
                    date.ToDateTime(
                        new TimeOnly(12, 0)),

                RankingDate =
                    date
            };

        snapshot.Performances.Add(
            new MkddPerformanceEntity
            {
                Player =
                    player,

                Time =
                    TimeSpan.FromMilliseconds(
                        milliseconds),

                Standard =
                    "Demo",

                RecordDate =
                    date
            });

        db.Snapshots.Add(
            snapshot);
    }
}