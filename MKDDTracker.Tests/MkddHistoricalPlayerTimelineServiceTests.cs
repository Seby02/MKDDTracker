using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalPlayerTimelineServiceTests
{
    [Fact]
    public async Task GetTimelineAsync_OrdersImprovementsCalculatesMovementAndCachesRankingByDay()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "timeline.db");

        try
        {
            await using var db = DatabaseTestSupport.CreateContext(databasePath);
            await db.Database.EnsureCreatedAsync();

            var course = new MkddCourse { Name = "Luigi Circuit" };
            var player = CreateModernPlayer(1, "Timeline Player");
            var alias = CreateAlias("YahooGroups", "Timeline Player", player);
            var opponent = CreateAlias("YahooGroups", "Opponent");
            DateTimeOffset jan1 = Utc(2020, 1, 1);
            DateTimeOffset jan2 = Utc(2020, 1, 2);
            DateTimeOffset jan3 = Utc(2020, 1, 3);
            DateTimeOffset jan4 = Utc(2020, 1, 4);
            DateTimeOffset jan5 = Utc(2020, 1, 5);

            db.HistoricalPerformances.AddRange(
                CreatePerformance("outside-before", alias, course, 100_000, jan1, 60, 1),
                CreatePerformance("same-day-first", alias, course, 96_000, jan2, 60, 2),
                CreatePerformance("same-day-second", alias, course, 95_000, jan2, 60, 3),
                CreatePerformance("day-two-opponent", opponent, course, 90_000, jan2, 60, 1),
                CreatePerformance("next-improvement", alias, course, 85_000, jan3, 60, 4),
                CreatePerformance("last-opponent", opponent, course, 75_000, jan4, 60, 2),
                CreatePerformance("last-improvement", alias, course, 80_000, jan4, 60, 5),
                CreatePerformance("outside-after", alias, course, 70_000, jan5, 60, 6));
            await db.SaveChangesAsync();

            var rankingDatesRequested = new List<DateOnly>();
            var service = new MkddHistoricalPlayerTimelineService(
                db,
                new MkddHistoricalRankingService(db),
                rankingDatesRequested.Add);
            MkddHistoricalPlayerTimeline? timeline = await service.GetTimelineAsync(
                CreateOption(alias, player),
                "Luigi Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.Hz60,
                jan2,
                Utc(2020, 1, 4, 23, 59, 59));

            Assert.NotNull(timeline);
            Assert.Equal(4, timeline.Points.Count);
            Assert.Equal(
                new[] { jan2, jan2, jan3, jan4 },
                timeline.Points.Select(point => point.Date).ToArray());
            Assert.Equal(TimeSpan.FromMilliseconds(96_000), timeline.Points[0].Time);
            Assert.Null(timeline.Points[0].PreviousTime);
            Assert.Null(timeline.Points[0].PreviousRank);
            Assert.Null(timeline.Points[0].TimeImprovement);
            Assert.Null(timeline.Points[0].PlacesGained);
            Assert.Equal(TimeSpan.FromMilliseconds(1_000), timeline.Points[1].TimeImprovement);
            Assert.Equal(0, timeline.Points[1].PlacesGained);
            Assert.Equal(TimeSpan.FromMilliseconds(10_000), timeline.Points[2].TimeImprovement);
            Assert.Equal(1, timeline.Points[2].PlacesGained);
            Assert.Equal(TimeSpan.FromMilliseconds(5_000), timeline.Points[3].TimeImprovement);
            Assert.Equal(-1, timeline.Points[3].PlacesGained);
            Assert.Equal(
                new[]
                {
                    new DateOnly(2020, 1, 2),
                    new DateOnly(2020, 1, 3),
                    new DateOnly(2020, 1, 4)
                },
                rankingDatesRequested);
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task GetTimelineAsync_MergesModernAliasesAndKeepsFrequenciesSeparate()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "timeline-aliases.db");

        try
        {
            await using var db = DatabaseTestSupport.CreateContext(databasePath);
            await db.Database.EnsureCreatedAsync();
            var course = new MkddCourse { Name = "Mario Circuit" };
            var modernPlayer = CreateModernPlayer(2, "Modern Player");
            var aliasOne = CreateAlias("YahooGroups", "Old Name", modernPlayer);
            var aliasTwo = CreateAlias("YahooGroups", "Another Old Name", modernPlayer);
            var historicalAlias = CreateAlias("YahooGroups", "Unlinked Player");
            DateTimeOffset date1 = Utc(2009, 10, 23);

            db.HistoricalPerformances.AddRange(
                CreatePerformance("modern-50", aliasOne, course, 100_000, date1, 50, 1),
                CreatePerformance("modern-60", aliasTwo, course, 95_000, date1.AddDays(1), 60, 1),
                CreatePerformance("modern-unknown", aliasOne, course, 90_000, date1.AddDays(2), null, 2),
                CreatePerformance("modern-second-60", aliasTwo, course, 94_000, date1.AddDays(3), 60, 2),
                CreatePerformance("unlinked", historicalAlias, course, 110_000, date1, 60, 1));
            await db.SaveChangesAsync();

            var service = new MkddHistoricalPlayerTimelineService(
                db,
                new MkddHistoricalRankingService(db));
            DateTimeOffset endDate = Utc(2009, 10, 30, 23, 59, 59);
            MkddHistoricalPlayerTimeline? combined = await service.GetTimelineAsync(
                CreateOption(aliasOne, modernPlayer),
                "Mario Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.All,
                date1,
                endDate);
            MkddHistoricalPlayerTimeline? only50 = await service.GetTimelineAsync(
                CreateOption(aliasOne, modernPlayer),
                "Mario Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.Hz50,
                date1,
                endDate);
            MkddHistoricalPlayerTimeline? only60 = await service.GetTimelineAsync(
                CreateOption(aliasOne, modernPlayer),
                "Mario Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.Hz60,
                date1,
                endDate);
            MkddHistoricalPlayerTimeline? onlyUnknown = await service.GetTimelineAsync(
                CreateOption(aliasOne, modernPlayer),
                "Mario Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.Unknown,
                date1,
                endDate);
            MkddHistoricalPlayerTimeline? unlinked = await service.GetTimelineAsync(
                CreateOption(historicalAlias),
                "Mario Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.Hz60,
                date1,
                endDate);

            Assert.NotNull(combined);
            Assert.Equal(4, combined.Points.Count);
            Assert.All(combined.Points, point => Assert.Equal(modernPlayer.Id, point.PlayerId));
            Assert.Single(Assert.IsType<MkddHistoricalPlayerTimeline>(only50).Points);
            Assert.Collection(
                Assert.IsType<MkddHistoricalPlayerTimeline>(only60).Points,
                _ => { },
                _ => { });
            Assert.Single(Assert.IsType<MkddHistoricalPlayerTimeline>(onlyUnknown).Points);
            Assert.Single(Assert.IsType<MkddHistoricalPlayerTimeline>(unlinked).Points);
            Assert.Null(unlinked!.Points[0].PlayerId);
            Assert.Equal(historicalAlias.Id, unlinked.Points[0].HistoricalPlayerAliasId);
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task GetTimelineAsync_ConsolidatesSameDayYahooAndModernObservationPreferringKnownYahooFrequency()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "timeline-cross-source.db");

        try
        {
            await using var db = DatabaseTestSupport.CreateContext(databasePath);
            await db.Database.EnsureCreatedAsync();
            var course = new MkddCourse { Name = "Luigi Circuit" };
            var player = CreateModernPlayer(3, "Sebastien Legay");
            var yahooAlias = CreateAlias("YahooGroups", "Sebastien Legay", player);
            var modernAlias = CreateAlias("MKDDTracker", "Sebastien Legay", player);
            DateTimeOffset yahooDate = Utc(2010, 8, 25, 12);
            DateTimeOffset modernDate = Utc(2010, 8, 25, 11);

            db.HistoricalPerformances.AddRange(
                CreatePerformance("yahoo-copy", yahooAlias, course, 75_369, yahooDate, 60, 10, "YahooGroups", "YahooFirstSeenDate"),
                CreatePerformance("modern-copy", modernAlias, course, 75_369, modernDate, null, 1, "MKDDTracker", "MKDDTrackerRecordDate"));
            await db.SaveChangesAsync();
            var rankingCheck = await new MkddHistoricalRankingService(db).GetRankingAtAsync(
                "Luigi Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.All,
                Utc(2010, 8, 25, 23, 59, 59));
            Assert.Contains(rankingCheck!.Entries, entry => entry.PlayerId == player.Id);

            var service = new MkddHistoricalPlayerTimelineService(
                db,
                new MkddHistoricalRankingService(db));
            MkddHistoricalPlayerTimeline? timeline = await service.GetTimelineAsync(
                CreateOption(yahooAlias, player),
                "Luigi Circuit",
                "ThreeLap",
                HistoricalFrequencyFilter.All,
                Utc(2010, 8, 25, 0),
                Utc(2010, 8, 25, 23, 59, 59));

            Assert.NotNull(timeline);
            var point = Assert.Single(timeline.Points);
            Assert.Equal("YahooGroups", point.SourceSystem);
            Assert.Equal("YahooFirstSeenDate", point.DateSource);
            Assert.Equal(yahooAlias.Id, point.HistoricalPlayerAliasId);
            Assert.Equal(new[] { "MKDDTracker", "YahooGroups" }, point.Sources);
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    private static MkddPlayer CreateModernPlayer(int sitePlayerId, string name) => new()
    {
        SitePlayerId = sitePlayerId,
        Name = name,
        Country = "FR"
    };

    private static MkddHistoricalPlayerAlias CreateAlias(
        string sourceSystem,
        string name,
        MkddPlayer? player = null) => new()
    {
        SourceSystem = sourceSystem,
        ExternalName = name,
        NormalizedExternalName = $"{sourceSystem}:{name.ToLowerInvariant()}",
        Player = player,
        Confidence = player is null ? 0 : 100,
        ResolutionStatus = player is null ? "Unresolved" : "Matched"
    };

    private static HistoricalPlayerOption CreateOption(
        MkddHistoricalPlayerAlias alias,
        MkddPlayer? player = null) => new()
    {
        Key = player is not null ? $"player:{player.Id}" : $"alias:{alias.Id}",
        DisplayName = player?.Name ?? alias.ExternalName,
        PlayerId = player?.Id,
        HistoricalPlayerAliasId = alias.Id
    };

    private static MkddHistoricalPerformance CreatePerformance(
        string key,
        MkddHistoricalPlayerAlias alias,
        MkddCourse course,
        long milliseconds,
        DateTimeOffset firstSeenAt,
        int? frequency,
        int sequenceNumber,
        string sourceSystem = "YahooGroups",
        string dateSource = "YahooFirstSeenDate") => new()
    {
        ExternalKey = key,
        HistoricalPlayerAlias = alias,
        Course = course,
        TimeType = "ThreeLap",
        Frequency = frequency,
        Time = TimeSpan.FromMilliseconds(milliseconds),
        FirstSeenAt = firstSeenAt,
        DateSource = dateSource,
        SequenceNumber = sequenceNumber,
        OccurrenceCount = 1,
        SourceSystem = sourceSystem,
        ImportStatus = "Approved"
    };

    private static DateTimeOffset Utc(
        int year,
        int month,
        int day,
        int hour = 12,
        int minute = 0,
        int second = 0) => new(year, month, day, hour, minute, second, TimeSpan.Zero);
}
