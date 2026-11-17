using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Services.Dashboard;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalDashboardServiceTests
{
    [Fact]
    public async Task GetDashboardAsync_ReturnsStatisticsAndConsolidatesSourceDuplicates()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "historical-dashboard.db");

        try
        {
            await using (var db = CreateContext(databasePath))
            {
                await db.Database.EnsureCreatedAsync();

                var player = new MkddPlayer
                {
                    SitePlayerId = 42,
                    Name = "Modern Player",
                    Country = "FR"
                };
                db.Players.Add(player);
                await db.SaveChangesAsync();

                var firstCourse = new MkddCourse { Name = "Luigi Circuit" };
                var secondCourse = new MkddCourse { Name = "Peach Beach" };
                var yahooAlias = CreateAlias("YahooGroups", "Yahoo Name", player.Id);
                var modernAlias = CreateAlias("MKDDTracker", "Modern Player", player.Id);
                var unlinkedAlias = CreateAlias("YahooGroups", "Unlinked Player", null);

                db.HistoricalPerformances.AddRange(
                    CreatePerformance(
                        "yahoo-observation", yahooAlias, firstCourse, "ThreeLap", 60,
                        74_000, new DateTimeOffset(2010, 1, 2, 12, 0, 0, TimeSpan.Zero),
                        "YahooGroups", 1),
                    CreatePerformance(
                        "modern-observation", modernAlias, firstCourse, "ThreeLap", null,
                        74_000, new DateTimeOffset(2010, 1, 2, 12, 30, 0, TimeSpan.Zero),
                        "MKDDTracker", 1),
                    CreatePerformance(
                        "flap-observation", modernAlias, firstCourse, "Flap", 50,
                        75_000, new DateTimeOffset(2011, 5, 6, 12, 0, 0, TimeSpan.Zero),
                        "YahooGroups", 2),
                    CreatePerformance(
                        "corrected-observation", unlinkedAlias, firstCourse, "ThreeLap", null,
                        76_000, new DateTimeOffset(2006, 4, 4, 12, 0, 0, TimeSpan.Zero),
                        "YahooGroups", 1,
                        dateSource: "SyntheticPre2003Correction"),
                    CreatePerformance(
                        "second-course-observation", modernAlias, secondCourse, "Flap", 60,
                        77_000, new DateTimeOffset(2012, 7, 8, 12, 0, 0, TimeSpan.Zero),
                        "MKDDTracker", 3));
                await db.SaveChangesAsync();
            }

            var service = new MkddHistoricalDashboardService(
                new TestDbContextFactory(databasePath));

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(5, dashboard.TotalPerformanceCount);
            Assert.Equal(2, dashboard.HistoricalIdentityCount);
            Assert.Equal(1, dashboard.ModernLinkedPlayerCount);
            Assert.Equal(2, dashboard.CourseCount);
            Assert.Equal(new DateTimeOffset(2006, 4, 4, 12, 0, 0, TimeSpan.Zero), dashboard.FirstSeenAt);
            Assert.Equal(new DateTimeOffset(2012, 7, 8, 12, 0, 0, TimeSpan.Zero), dashboard.LastSeenAt);
            Assert.Equal(3, dashboard.YahooGroupsCount);
            Assert.Equal(2, dashboard.MkddTrackerCount);
            Assert.Equal(3, dashboard.ThreeLapCount);
            Assert.Equal(2, dashboard.FlapCount);
            Assert.Equal(1, dashboard.Hz50Count);
            Assert.Equal(2, dashboard.Hz60Count);
            Assert.Equal(2, dashboard.UnknownFrequencyCount);
            Assert.Equal(4, dashboard.RecentEvents.Count);
            Assert.Equal("Peach Beach", dashboard.RecentEvents[0].CourseName);

            var consolidatedEvent = Assert.Single(dashboard.RecentEvents, item =>
                item.CourseName == "Luigi Circuit" && item.Time == TimeSpan.FromMilliseconds(74_000));
            Assert.Equal("player:" + (await GetPlayerIdAsync(databasePath)), consolidatedEvent.PlayerKey);
            Assert.Equal(60, consolidatedEvent.Frequency);
            Assert.Equal(new[] { "MKDDTracker", "YahooGroups" }, consolidatedEvent.Sources);
        }
        finally
        {
            DeleteDatabaseDirectory(directory);
        }
    }

    [Fact]
    public async Task GetDashboardAsync_RecentEventsAreOrderedInMemoryAndLimitedToTen()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "historical-dashboard-recent.db");

        try
        {
            await using (var db = CreateContext(databasePath))
            {
                await db.Database.EnsureCreatedAsync();
                var course = new MkddCourse { Name = "Luigi Circuit" };
                var alias = CreateAlias("YahooGroups", "Player", null);
                for (int day = 1; day <= 12; day++)
                {
                    db.HistoricalPerformances.Add(CreatePerformance(
                        $"recent-{day}", alias, course, "ThreeLap", 60,
                        70_000 + day,
                        new DateTimeOffset(2020, 1, day, 12, 0, 0, TimeSpan.Zero),
                        "YahooGroups", day));
                }

                await db.SaveChangesAsync();
            }

            var service = new MkddHistoricalDashboardService(
                new TestDbContextFactory(databasePath));

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(10, dashboard.RecentEvents.Count);
            Assert.Equal(new DateTimeOffset(2020, 1, 12, 12, 0, 0, TimeSpan.Zero), dashboard.RecentEvents[0].FirstSeenAt);
            Assert.Equal(new DateTimeOffset(2020, 1, 3, 12, 0, 0, TimeSpan.Zero), dashboard.RecentEvents[^1].FirstSeenAt);
        }
        finally
        {
            DeleteDatabaseDirectory(directory);
        }
    }

    [Fact]
    public async Task GetDashboardAsync_WithoutData_ReturnsZeroCountsAndNoDatesOrEvents()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "historical-dashboard-empty.db");

        try
        {
            await using (var db = CreateContext(databasePath))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var service = new MkddHistoricalDashboardService(
                new TestDbContextFactory(databasePath));

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(0, dashboard.TotalPerformanceCount);
            Assert.Equal(0, dashboard.HistoricalIdentityCount);
            Assert.Equal(0, dashboard.ModernLinkedPlayerCount);
            Assert.Equal(0, dashboard.CourseCount);
            Assert.Null(dashboard.FirstSeenAt);
            Assert.Null(dashboard.LastSeenAt);
            Assert.Equal(0, dashboard.YahooGroupsCount);
            Assert.Equal(0, dashboard.MkddTrackerCount);
            Assert.Equal(0, dashboard.ThreeLapCount);
            Assert.Equal(0, dashboard.FlapCount);
            Assert.Equal(0, dashboard.Hz50Count);
            Assert.Equal(0, dashboard.Hz60Count);
            Assert.Equal(0, dashboard.UnknownFrequencyCount);
            Assert.Empty(dashboard.RecentEvents);
        }
        finally
        {
            DeleteDatabaseDirectory(directory);
        }
    }

    private static MkddHistoricalPlayerAlias CreateAlias(
        string sourceSystem,
        string name,
        int? playerId) => new()
    {
        SourceSystem = sourceSystem,
        ExternalName = name,
        NormalizedExternalName = $"{sourceSystem}-{name}".ToLowerInvariant(),
        PlayerId = playerId,
        Confidence = playerId is null ? 0 : 100,
        ResolutionStatus = playerId is null ? "Unresolved" : "Matched"
    };

    private static MkddHistoricalPerformance CreatePerformance(
        string externalKey,
        MkddHistoricalPlayerAlias alias,
        MkddCourse course,
        string timeType,
        int? frequency,
        int milliseconds,
        DateTimeOffset firstSeenAt,
        string sourceSystem,
        int sequenceNumber,
        string dateSource = "YahooFirstSeenDate") => new()
    {
        ExternalKey = externalKey,
        HistoricalPlayerAlias = alias,
        Course = course,
        TimeType = timeType,
        Frequency = frequency,
        Time = TimeSpan.FromMilliseconds(milliseconds),
        FirstSeenAt = firstSeenAt,
        DateSource = dateSource,
        SequenceNumber = sequenceNumber,
        OccurrenceCount = 1,
        SourceSystem = sourceSystem,
        ImportStatus = "Approved"
    };

    private static async Task<int> GetPlayerIdAsync(string databasePath)
    {
        await using var db = CreateContext(databasePath);
        return await db.Players.Select(player => player.Id).SingleAsync();
    }

    private static MkddDbContext CreateContext(string databasePath)
    {
        var options = new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite($"Data Source={databasePath};Pooling=False")
            .Options;
        return new MkddDbContext(options);
    }

    private static void DeleteDatabaseDirectory(string directory) =>
        Directory.Delete(directory, recursive: true);

    private sealed class TestDbContextFactory(string databasePath)
        : IDbContextFactory<MkddDbContext>
    {
        public MkddDbContext CreateDbContext() =>
            CreateContext(databasePath);

        public Task<MkddDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
