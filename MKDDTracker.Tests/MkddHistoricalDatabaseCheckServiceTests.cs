using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Services.Database;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalDatabaseCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_HealthyFreshlyMigratedDatabaseWithoutPerformances()
    {
        await using var fixture = await TestDatabase.CreateMigratedAsync();
        var service = new MkddHistoricalDatabaseCheckService(fixture.Factory);

        MkddHistoricalDatabaseCheckResult result = await service.CheckAsync();

        Assert.Equal(HistoricalDatabaseHealthStatus.Healthy, result.Status);
        Assert.True(result.DatabaseAccessible);
        Assert.Empty(result.PendingMigrations);
        Assert.Equal(0, result.Historical.HistoricalPerformanceCount);
        Assert.Equal(0, result.Historical.SourceCount);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task CheckAsync_SnapshotDataDoesNotAffectHealthyHistoricalStatus()
    {
        await using var fixture = await TestDatabase.CreateMigratedAsync();
        await using (var db = fixture.Factory.CreateDbContext())
        {
            var course = new MkddCourse { Name = "Luigi Circuit" };
            var player = new MkddPlayer { SitePlayerId = 42, Name = "Legacy Player", Country = "FR" };
            var snapshot = new MkddSnapshot
            {
                Course = course,
                CapturedAt = new DateTime(2026, 9, 15, 12, 0, 0),
                RankingDate = new DateOnly(2026, 9, 15)
            };
            snapshot.Performances.Add(new MkddPerformanceEntity
            {
                Player = player,
                Rank = 1,
                Time = TimeSpan.FromSeconds(75),
                Standard = "God",
                RecordDate = new DateOnly(2026, 9, 14)
            });
            db.Snapshots.Add(snapshot);
            await db.SaveChangesAsync();
        }

        MkddHistoricalDatabaseCheckResult result = await new MkddHistoricalDatabaseCheckService(fixture.Factory)
            .CheckAsync();

        Assert.Equal(HistoricalDatabaseHealthStatus.Healthy, result.Status);
        Assert.Empty(result.Warnings);
        Assert.Equal(0, result.Historical.HistoricalPerformanceCount);
    }

    [Fact]
    public async Task CheckAsync_CountsHistoricalSourcesAndMetadataCategories()
    {
        await using var fixture = await TestDatabase.CreateMigratedAsync();
        await SeedHistoryAsync(fixture, includeSources: true);

        MkddHistoricalDatabaseCheckResult result = await new MkddHistoricalDatabaseCheckService(fixture.Factory)
            .CheckAsync();

        Assert.Equal(HistoricalDatabaseHealthStatus.Healthy, result.Status);
        Assert.Equal(1, result.Historical.HistoricalCourseCount);
        Assert.Equal(3, result.Historical.HistoricalAliasCount);
        Assert.Equal(1, result.Historical.LinkedModernPlayerCount);
        Assert.Equal(3, result.Historical.HistoricalPerformanceCount);
        Assert.Equal(3, result.Historical.SourceCount);
        Assert.Equal(2, result.Historical.YahooGroupsCount);
        Assert.Equal(1, result.Historical.MkddTrackerCount);
        Assert.Equal(2, result.Historical.ThreeLapCount);
        Assert.Equal(1, result.Historical.FlapCount);
        Assert.Equal(1, result.Historical.FiftyHzCount);
        Assert.Equal(1, result.Historical.SixtyHzCount);
        Assert.Equal(1, result.Historical.UnknownFrequencyCount);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero), result.Historical.FirstSeenAt);
        Assert.Equal(new DateTimeOffset(2024, 1, 3, 12, 0, 0, TimeSpan.Zero), result.Historical.LastSeenAt);
    }

    [Fact]
    public async Task CheckAsync_ReportsInvalidExternalKeyTimeTypeFrequencyAndPrMetadata()
    {
        await using var fixture = await TestDatabase.CreateMigratedAsync();
        await using (var db = fixture.Factory.CreateDbContext())
        {
            var course = new MkddCourse { Name = "Luigi Circuit" };
            var player = new MkddPlayer { SitePlayerId = 42, Name = "Racer", Country = "FR" };
            var alias = CreateAlias(player, "Racer", "racer");
            db.HistoricalPerformances.AddRange(
                CreatePerformance(alias, course, "", TimeSpan.FromSeconds(75), "ThreeLap", 50),
                CreatePerformance(alias, course, "invalid-time", TimeSpan.Zero, "Flap", 60),
                CreatePerformance(alias, course, "invalid-type", TimeSpan.FromSeconds(75), "Unknown", 59),
                CreatePerformance(
                    alias,
                    course,
                    "incoherent-pr",
                    TimeSpan.FromSeconds(75),
                    "ThreeLap",
                    null,
                    previousTime: TimeSpan.FromSeconds(80),
                    improvement: TimeSpan.FromSeconds(4)),
                CreatePerformance(
                    alias,
                    course,
                    "negative-improvement",
                    TimeSpan.FromSeconds(75),
                    "ThreeLap",
                    null,
                    previousTime: TimeSpan.FromSeconds(80),
                    improvement: TimeSpan.FromSeconds(-5)));
            await db.SaveChangesAsync();
        }

        MkddHistoricalDatabaseCheckResult result = await new MkddHistoricalDatabaseCheckService(fixture.Factory)
            .CheckAsync();

        Assert.Equal(HistoricalDatabaseHealthStatus.Error, result.Status);
        Assert.Equal(1, result.Integrity.EmptyExternalKeys);
        Assert.Equal(1, result.Integrity.NonPositiveTimes);
        Assert.Equal(1, result.Integrity.InvalidTimeTypes);
        Assert.Equal(1, result.Integrity.InvalidFrequencies);
        Assert.Equal(1, result.Integrity.NegativeApprovedImprovements);
        Assert.Equal(2, result.Integrity.IncoherentApprovedPrMetadata);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public async Task CheckAsync_MatchedAliasWithoutPlayerIsAnError()
    {
        await using var fixture = await TestDatabase.CreateMigratedAsync();
        await using (var db = fixture.Factory.CreateDbContext())
        {
            db.HistoricalPlayerAliases.Add(CreateAlias(null, "Matched without player", "matched-without-player"));
            await db.SaveChangesAsync();
        }

        MkddHistoricalDatabaseCheckResult result = await new MkddHistoricalDatabaseCheckService(fixture.Factory)
            .CheckAsync();

        Assert.Equal(HistoricalDatabaseHealthStatus.Error, result.Status);
        Assert.Equal(1, result.Integrity.MatchedAliasesWithoutPlayer);
    }

    [Fact]
    public async Task CheckAsync_PendingMigrationProducesErrorStatus()
    {
        await using var fixture = await TestDatabase.CreateMigratedAsync();
        await using (var db = fixture.Factory.CreateDbContext())
        {
            string latestMigration = (await db.Database.GetAppliedMigrationsAsync()).Last();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM __EFMigrationsHistory WHERE MigrationId = {latestMigration}");
        }

        MkddHistoricalDatabaseCheckResult result = await new MkddHistoricalDatabaseCheckService(fixture.Factory)
            .CheckAsync();

        Assert.Equal(HistoricalDatabaseHealthStatus.Error, result.Status);
        Assert.NotEmpty(result.PendingMigrations);
        Assert.Contains(result.PendingMigrations, migration => migration.Length > 0);
    }

    [Fact]
    public async Task CheckAsync_ReportsDuplicateExternalKeys()
    {
        await using var fixture = await TestDatabase.CreateMigratedAsync();
        await using (var db = fixture.Factory.CreateDbContext())
        {
            await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_HistoricalPerformances_ExternalKey;");
            var course = new MkddCourse { Name = "Luigi Circuit" };
            var player = new MkddPlayer { SitePlayerId = 42, Name = "Racer", Country = "FR" };
            var alias = CreateAlias(player, "Racer", "racer");
            db.HistoricalPerformances.AddRange(
                CreatePerformance(alias, course, "duplicate-key", TimeSpan.FromSeconds(75), "ThreeLap", null),
                CreatePerformance(alias, course, "duplicate-key", TimeSpan.FromSeconds(74), "ThreeLap", null));
            await db.SaveChangesAsync();
        }

        MkddHistoricalDatabaseCheckResult result = await new MkddHistoricalDatabaseCheckService(fixture.Factory)
            .CheckAsync();

        Assert.Equal(HistoricalDatabaseHealthStatus.Error, result.Status);
        Assert.Equal(1, result.Integrity.DuplicateExternalKeyGroups);
    }

    private static async Task SeedHistoryAsync(TestDatabase fixture, bool includeSources)
    {
        await using var db = fixture.Factory.CreateDbContext();
        var player = new MkddPlayer { SitePlayerId = 42, Name = "Racer", Country = "FR" };
        var course = new MkddCourse { Name = "Luigi Circuit" };
        MkddHistoricalPlayerAlias[] aliases =
        [
            CreateAlias(player, "Yahoo Racer One", "yahoo-racer-one", "YahooGroups"),
            CreateAlias(player, "Yahoo Racer Two", "yahoo-racer-two", "YahooGroups"),
            CreateAlias(player, "Modern Racer", "modern-racer", "MKDDTracker")
        ];
        DateTimeOffset firstSeenAt = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        MkddHistoricalPerformance[] performances =
        [
            CreatePerformance(aliases[0], course, "yahoo-1", TimeSpan.FromSeconds(75), "ThreeLap", 50, firstSeenAt),
            CreatePerformance(aliases[1], course, "yahoo-2", TimeSpan.FromSeconds(76), "ThreeLap", 60, firstSeenAt.AddDays(1)),
            CreatePerformance(aliases[2], course, "modern-1", TimeSpan.FromSeconds(30), "Flap", null, firstSeenAt.AddDays(2), sourceSystem: "MKDDTracker")
        ];

        if (includeSources)
        {
            for (int index = 0; index < performances.Length; index++)
            {
                performances[index].Sources.Add(new MkddHistoricalPerformanceSource
                {
                    MessageId = 1000 + index,
                    PublishedAt = firstSeenAt.AddDays(index),
                    SourceFile = $"source-{index}.html",
                    OriginalTime = "01'15\"000",
                    SourceLine = "historical record",
                    PlayerConfidence = 100,
                    TimeTypeConfidence = 100,
                    ImportStatus = "Approved"
                });
            }
        }

        db.HistoricalPerformances.AddRange(performances);
        await db.SaveChangesAsync();
    }

    private static MkddHistoricalPlayerAlias CreateAlias(
        MkddPlayer? player,
        string name,
        string normalizedName,
        string sourceSystem = "YahooGroups") =>
        new()
        {
            SourceSystem = sourceSystem,
            ExternalName = name,
            NormalizedExternalName = normalizedName,
            Player = player,
            Confidence = 100,
            ResolutionStatus = "Matched"
        };

    private static MkddHistoricalPerformance CreatePerformance(
        MkddHistoricalPlayerAlias alias,
        MkddCourse course,
        string externalKey,
        TimeSpan time,
        string timeType,
        int? frequency,
        DateTimeOffset? firstSeenAt = null,
        TimeSpan? previousTime = null,
        TimeSpan? improvement = null,
        string sourceSystem = "YahooGroups") =>
        new()
        {
            ExternalKey = externalKey,
            HistoricalPlayerAlias = alias,
            Course = course,
            TimeType = timeType,
            Frequency = frequency,
            Time = time,
            FirstSeenAt = firstSeenAt ?? new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
            DateSource = "RecordDate",
            SequenceNumber = previousTime.HasValue ? 2 : 1,
            PreviousTime = previousTime,
            Improvement = improvement,
            OccurrenceCount = 1,
            SourceSystem = sourceSystem,
            ImportStatus = "Approved"
        };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly string _directory;

        private TestDatabase(string directory, TestDbContextFactory factory)
        {
            _directory = directory;
            Factory = factory;
        }

        public TestDbContextFactory Factory { get; }

        public static async Task<TestDatabase> CreateMigratedAsync()
        {
            string directory = DatabaseTestSupport.CreateTemporaryDirectory();
            string databasePath = Path.Combine(directory, "historical-check.db");
            var options = new DbContextOptionsBuilder<MkddDbContext>()
                .UseSqlite($"Data Source={databasePath};Pooling=False")
                .Options;
            var database = new TestDatabase(directory, new TestDbContextFactory(options));
            await using var db = database.Factory.CreateDbContext();
            await db.Database.MigrateAsync();
            return database;
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<MkddDbContext> options)
        : IDbContextFactory<MkddDbContext>
    {
        public MkddDbContext CreateDbContext() => new(options);

        public Task<MkddDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
