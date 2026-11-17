using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalPlayerTimelineRulesTests
{
    [Fact]
    public async Task FirstPoint_HasNoPreviousComparisonValues()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("first", alias, 90_000, At(2020, 1, 1), 60, 1);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            alias,
            null,
            HistoricalFrequencyFilter.Hz60);
        MkddHistoricalPlayerTimelinePoint point = Assert.Single(timeline.Points);

        Assert.Null(point.PreviousTime);
        Assert.Null(point.PreviousRank);
        Assert.Null(point.TimeImprovement);
        Assert.Null(point.PlacesGained);
    }

    [Fact]
    public async Task Timeline_SortsByFirstSeenAtThenSequenceNumber()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("insert-first", alias, 85_000, At(2020, 1, 1, 13), 60, 3);
        fixture.AddPerformance("insert-second", alias, 90_000, At(2020, 1, 1, 12), 60, 2);
        fixture.AddPerformance("insert-third", alias, 95_000, At(2020, 1, 1, 12), 60, 1);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            alias,
            null,
            HistoricalFrequencyFilter.Hz60);

        Assert.Equal(new[] { 1, 2, 3 }, timeline.Points.Select(point => point.SequenceNumber));
        Assert.Equal(
            new[] { 95_000, 90_000, 85_000 },
            timeline.Points.Select(point => (int)point.Time.TotalMilliseconds));
    }

    [Fact]
    public async Task Timeline_ExcludesPerformancesOutsideRequestedPeriod()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("before", alias, 100_000, At(2020, 1, 1), 60, 1);
        fixture.AddPerformance("inside", alias, 90_000, At(2020, 1, 2), 60, 2);
        fixture.AddPerformance("after", alias, 80_000, At(2020, 1, 3), 60, 3);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            alias,
            null,
            HistoricalFrequencyFilter.Hz60,
            StartOf(2020, 1, 2),
            EndOf(2020, 1, 2));

        var point = Assert.Single(timeline.Points);
        Assert.Equal(TimeSpan.FromMilliseconds(90_000), point.Time);
        Assert.Equal(At(2020, 1, 2), point.Date);
    }

    [Fact]
    public async Task Timeline_DateBoundsIncludeWholeStartAndEndDays()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("start-day-morning", alias, 95_000, At(2020, 1, 2, 9), 60, 1);
        fixture.AddPerformance("end-day-evening", alias, 90_000, At(2020, 1, 3, 21), 60, 2);
        fixture.AddPerformance("after-end-day", alias, 85_000, At(2020, 1, 4, 9), 60, 3);
        await fixture.SaveChangesAsync();

        DateTimeOffset startAtEndOfDay = EndOf(2020, 1, 2);
        DateTimeOffset endAtEndOfDay = EndOf(2020, 1, 3);
        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            alias,
            null,
            HistoricalFrequencyFilter.Hz60,
            startAtEndOfDay,
            endAtEndOfDay);

        Assert.Equal(2, timeline.Points.Count);
        Assert.Equal(
            new[] { At(2020, 1, 2, 9), At(2020, 1, 3, 21) },
            timeline.Points.Select(point => point.Date));
    }

    [Fact]
    public async Task Timeline_KeepsFiftyAndSixtyHertzSeriesSeparate()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("50hz", alias, 100_000, At(2020, 1, 1), 50, 1);
        fixture.AddPerformance("60hz", alias, 90_000, At(2020, 1, 2), 60, 2);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline hz50 = await fixture.GetTimelineAsync(
            alias, null, HistoricalFrequencyFilter.Hz50);
        MkddHistoricalPlayerTimeline hz60 = await fixture.GetTimelineAsync(
            alias, null, HistoricalFrequencyFilter.Hz60);

        Assert.Equal(50, Assert.Single(hz50.Points).Frequency);
        Assert.Equal(60, Assert.Single(hz60.Points).Frequency);
        Assert.Equal(TimeSpan.FromMilliseconds(100_000), hz50.Points[0].Time);
        Assert.Equal(TimeSpan.FromMilliseconds(90_000), hz60.Points[0].Time);
    }

    [Fact]
    public async Task UnknownFrequencyFilter_ExcludesModernObservationEnrichedByYahooFrequency()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddPlayer player = await fixture.AddModernPlayerAsync("Player", 21);
        MkddHistoricalPlayerAlias modernAlias = fixture.AddAlias("Player", "MKDDTracker", player);
        MkddHistoricalPlayerAlias yahooAlias = fixture.AddAlias("Player", "YahooGroups", player);
        fixture.AddPerformance("unknown-copy", modernAlias, 80_000, At(2020, 1, 1), null, 1, "MKDDTracker");
        fixture.AddPerformance("known-copy", yahooAlias, 80_000, At(2020, 1, 1, 13), 60, 1, "YahooGroups");
        fixture.AddPerformance("unknown-only", modernAlias, 90_000, At(2020, 1, 2), null, 2, "MKDDTracker");
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            modernAlias,
            player,
            HistoricalFrequencyFilter.Unknown);

        var point = Assert.Single(timeline.Points);
        Assert.Equal(TimeSpan.FromMilliseconds(90_000), point.Time);
        Assert.Null(point.Frequency);
        Assert.Equal(At(2020, 1, 2), point.Date);
    }

    [Fact]
    public async Task Timeline_MergesPerformancesFromMultipleAliasesForSamePlayerId()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddPlayer player = await fixture.AddModernPlayerAsync("Modern Player", 22);
        MkddHistoricalPlayerAlias aliasOne = fixture.AddAlias("First Alias", "YahooGroups", player);
        MkddHistoricalPlayerAlias aliasTwo = fixture.AddAlias("Second Alias", "YahooGroups", player);
        fixture.AddPerformance("alias-one", aliasOne, 100_000, At(2020, 1, 1), 60, 1);
        fixture.AddPerformance("alias-two", aliasTwo, 90_000, At(2020, 1, 2), 60, 1);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            aliasOne,
            player,
            HistoricalFrequencyFilter.Hz60);

        Assert.Equal(2, timeline.Points.Count);
        Assert.All(timeline.Points, point => Assert.Equal(player.Id, point.PlayerId));
        Assert.Equal(
            new[] { aliasOne.Id, aliasTwo.Id },
            timeline.Points.Select(point => point.HistoricalPlayerAliasId));
    }

    [Fact]
    public async Task UnlinkedAliasesWithSameDisplayName_RemainSeparateIdentities()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias yahooAlias = fixture.AddAlias("Same Name", "YahooGroups");
        MkddHistoricalPlayerAlias otherAlias = fixture.AddAlias("Same Name", "OtherArchive");
        fixture.AddPerformance("same-name-yahoo", yahooAlias, 90_000, At(2020, 1, 1), 60, 1);
        fixture.AddPerformance("same-name-other", otherAlias, 90_000, At(2020, 1, 1), 60, 1);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline yahooTimeline = await fixture.GetTimelineAsync(
            yahooAlias,
            null,
            HistoricalFrequencyFilter.Hz60);
        MkddHistoricalPlayerTimeline otherTimeline = await fixture.GetTimelineAsync(
            otherAlias,
            null,
            HistoricalFrequencyFilter.Hz60);

        var yahooPoint = Assert.Single(yahooTimeline.Points);
        var otherPoint = Assert.Single(otherTimeline.Points);
        Assert.Equal(yahooAlias.ExternalName, yahooTimeline.PlayerName);
        Assert.Equal(otherAlias.ExternalName, otherTimeline.PlayerName);
        Assert.NotEqual(yahooPoint.HistoricalPlayerAliasId, otherPoint.HistoricalPlayerAliasId);
        Assert.Null(yahooPoint.PlayerId);
        Assert.Null(otherPoint.PlayerId);
    }

    [Fact]
    public async Task CrossSourceDuplicateOnSameUtcDate_ProducesOnePoint()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddPlayer player = await fixture.AddModernPlayerAsync("Player", 23);
        MkddHistoricalPlayerAlias yahooAlias = fixture.AddAlias("Player", "YahooGroups", player);
        MkddHistoricalPlayerAlias modernAlias = fixture.AddAlias("Player", "MKDDTracker", player);
        fixture.AddPerformance("cross-yahoo", yahooAlias, 75_369, At(2020, 1, 1, 12), 60, 1, "YahooGroups");
        fixture.AddPerformance("cross-modern", modernAlias, 75_369, At(2020, 1, 1, 11), null, 1, "MKDDTracker");
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            yahooAlias,
            player,
            HistoricalFrequencyFilter.All);

        var point = Assert.Single(timeline.Points);
        Assert.Equal(TimeSpan.FromMilliseconds(75_369), point.Time);
    }

    [Fact]
    public async Task CrossSourceDuplicate_PreservesKnownFrequencyAndDistinctSources()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddPlayer player = await fixture.AddModernPlayerAsync("Player", 24);
        MkddHistoricalPlayerAlias yahooAlias = fixture.AddAlias("Player", "YahooGroups", player);
        MkddHistoricalPlayerAlias modernAlias = fixture.AddAlias("Player", "MKDDTracker", player);
        fixture.AddPerformance("sources-yahoo", yahooAlias, 75_369, At(2020, 1, 1, 12), 60, 1, "YahooGroups", "YahooFirstSeenDate");
        fixture.AddPerformance("sources-modern", modernAlias, 75_369, At(2020, 1, 1, 11), null, 1, "MKDDTracker", "MKDDTrackerRecordDate");
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            yahooAlias,
            player,
            HistoricalFrequencyFilter.All);

        var point = Assert.Single(timeline.Points);
        Assert.Equal(60, point.Frequency);
        Assert.Equal("YahooGroups", point.SourceSystem);
        Assert.Equal(new[] { "MKDDTracker", "YahooGroups" }, point.Sources);
    }

    [Fact]
    public async Task SameChronoOnDifferentUtcCivilDates_ProducesTwoPoints()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("same-time-day-one", alias, 75_369, At(2020, 1, 1), 60, 1);
        fixture.AddPerformance("same-time-day-two", alias, 75_369, At(2020, 1, 2), 60, 2);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            alias,
            null,
            HistoricalFrequencyFilter.Hz60);

        Assert.Equal(2, timeline.Points.Count);
        Assert.Equal(timeline.Points[0].Time, timeline.Points[1].Time);
        Assert.Equal(new[] { At(2020, 1, 1), At(2020, 1, 2) }, timeline.Points.Select(point => point.Date));
    }

    [Fact]
    public async Task DifferentChronosOnSameDay_RemainSeparateAndSortByTimeThenSequence()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("late-seq-three", alias, 85_000, At(2020, 1, 1, 13), 60, 3);
        fixture.AddPerformance("early-seq-two", alias, 90_000, At(2020, 1, 1, 12), 60, 2);
        fixture.AddPerformance("early-seq-one", alias, 95_000, At(2020, 1, 1, 12), 60, 1);
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            alias,
            null,
            HistoricalFrequencyFilter.Hz60);

        Assert.Equal(new[] { 1, 2, 3 }, timeline.Points.Select(point => point.SequenceNumber));
        Assert.Equal(
            new[] { 95_000, 90_000, 85_000 },
            timeline.Points.Select(point => (int)point.Time.TotalMilliseconds));
    }

    [Fact]
    public async Task MultipleEventsOnSameDay_ReconstructRankingOnlyOnce()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddHistoricalPlayerAlias alias = fixture.AddAlias("Player");
        fixture.AddPerformance("cache-one", alias, 100_000, At(2020, 1, 1, 10), 60, 1);
        fixture.AddPerformance("cache-two", alias, 95_000, At(2020, 1, 1, 11), 60, 2);
        fixture.AddPerformance("cache-three", alias, 90_000, At(2020, 1, 1, 12), 60, 3);
        await fixture.SaveChangesAsync();
        var reconstructedDates = new List<DateOnly>();
        var service = new MkddHistoricalPlayerTimelineService(
            fixture.Db,
            new MkddHistoricalRankingService(fixture.Db),
            reconstructedDates.Add);

        MkddHistoricalPlayerTimeline? timeline = await service.GetTimelineAsync(
            fixture.CreateOption(alias, null),
            fixture.Course.Name,
            "ThreeLap",
            HistoricalFrequencyFilter.Hz60,
            StartOf(2020, 1, 1),
            EndOf(2020, 1, 1));

        Assert.NotNull(timeline);
        Assert.Equal(3, timeline.Points.Count);
        Assert.Equal(new[] { new DateOnly(2020, 1, 1) }, reconstructedDates);
    }

    [Fact]
    public async Task FrequencyFilterRunsAfterConsolidationAndIncludesEnrichedYahooFrequency()
    {
        await using var fixture = await TimelineFixture.CreateAsync();
        MkddPlayer player = await fixture.AddModernPlayerAsync("Player", 25);
        MkddHistoricalPlayerAlias modernAlias = fixture.AddAlias("Player", "MKDDTracker", player);
        MkddHistoricalPlayerAlias yahooAlias = fixture.AddAlias("Player", "YahooGroups", player);
        fixture.AddPerformance("enrich-modern", modernAlias, 80_000, At(2020, 1, 1, 10), null, 1, "MKDDTracker");
        fixture.AddPerformance("enrich-yahoo", yahooAlias, 80_000, At(2020, 1, 1, 11), 60, 1, "YahooGroups");
        await fixture.SaveChangesAsync();

        MkddHistoricalPlayerTimeline timeline = await fixture.GetTimelineAsync(
            modernAlias,
            player,
            HistoricalFrequencyFilter.Hz60);
        MkddHistoricalPlayerTimeline? unknownTimeline = await fixture.GetTimelineNullableAsync(
            modernAlias,
            player,
            HistoricalFrequencyFilter.Unknown);

        var point = Assert.Single(timeline.Points);
        Assert.Equal(60, point.Frequency);
        Assert.Equal(new[] { "MKDDTracker", "YahooGroups" }, point.Sources);
        Assert.Null(unknownTimeline);
    }

    private static DateTimeOffset At(int year, int month, int day, int hour = 12) =>
        new(year, month, day, hour, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset StartOf(int year, int month, int day) =>
        new(new DateOnly(year, month, day).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    private static DateTimeOffset EndOf(int year, int month, int day) =>
        new(new DateOnly(year, month, day).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

    private sealed class TimelineFixture : IAsyncDisposable
    {
        private readonly string _directory;

        private TimelineFixture(string directory, MkddDbContext db, MkddCourse course)
        {
            _directory = directory;
            Db = db;
            Course = course;
        }

        public MkddDbContext Db { get; }

        public MkddCourse Course { get; }

        public static async Task<TimelineFixture> CreateAsync()
        {
            string directory = DatabaseTestSupport.CreateTemporaryDirectory();
            MkddDbContext db = DatabaseTestSupport.CreateContext(
                Path.Combine(directory, "timeline-rules.db"));
            await db.Database.EnsureCreatedAsync();
            var course = new MkddCourse { Name = "Luigi Circuit" };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            return new TimelineFixture(directory, db, course);
        }

        public async Task<MkddPlayer> AddModernPlayerAsync(string name, int sitePlayerId)
        {
            var player = new MkddPlayer
            {
                Name = name,
                SitePlayerId = sitePlayerId,
                Country = "FR"
            };
            Db.Players.Add(player);
            await Db.SaveChangesAsync();
            return player;
        }

        public MkddHistoricalPlayerAlias AddAlias(
            string name,
            string sourceSystem = "YahooGroups",
            MkddPlayer? player = null)
        {
            var alias = new MkddHistoricalPlayerAlias
            {
                SourceSystem = sourceSystem,
                ExternalName = name,
                NormalizedExternalName = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}",
                Player = player,
                Confidence = player is null ? 0 : 100,
                ResolutionStatus = player is null ? "Unresolved" : "Matched"
            };
            Db.HistoricalPlayerAliases.Add(alias);
            return alias;
        }

        public void AddPerformance(
            string key,
            MkddHistoricalPlayerAlias alias,
            long milliseconds,
            DateTimeOffset date,
            int? frequency,
            int sequenceNumber,
            string? sourceSystem = null,
            string dateSource = "YahooFirstSeenDate")
        {
            Db.HistoricalPerformances.Add(new MkddHistoricalPerformance
            {
                ExternalKey = key,
                HistoricalPlayerAlias = alias,
                Course = Course,
                TimeType = "ThreeLap",
                Frequency = frequency,
                Time = TimeSpan.FromMilliseconds(milliseconds),
                FirstSeenAt = date,
                DateSource = dateSource,
                SequenceNumber = sequenceNumber,
                OccurrenceCount = 1,
                SourceSystem = sourceSystem ?? alias.SourceSystem,
                ImportStatus = "Approved"
            });
        }

        public Task SaveChangesAsync() => Db.SaveChangesAsync();

        public HistoricalPlayerOption CreateOption(
            MkddHistoricalPlayerAlias alias,
            MkddPlayer? player) => new()
        {
            Key = player is not null ? $"player:{player.Id}" : $"alias:{alias.Id}",
            DisplayName = player?.Name ?? alias.ExternalName,
            PlayerId = player?.Id,
            HistoricalPlayerAliasId = alias.Id
        };

        public Task<MkddHistoricalPlayerTimeline?> GetTimelineNullableAsync(
            MkddHistoricalPlayerAlias alias,
            MkddPlayer? player,
            HistoricalFrequencyFilter filter) => new MkddHistoricalPlayerTimelineService(
                Db,
                new MkddHistoricalRankingService(Db))
            .GetTimelineAsync(
                CreateOption(alias, player),
                Course.Name,
                "ThreeLap",
                filter,
                StartOf(2010, 1, 1),
                EndOf(2030, 12, 31));

        public async Task<MkddHistoricalPlayerTimeline> GetTimelineAsync(
            MkddHistoricalPlayerAlias alias,
            MkddPlayer? player,
            HistoricalFrequencyFilter filter,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null)
        {
            MkddHistoricalPlayerTimeline? timeline = await new MkddHistoricalPlayerTimelineService(
                    Db,
                    new MkddHistoricalRankingService(Db))
                .GetTimelineAsync(
                    CreateOption(alias, player),
                    Course.Name,
                    "ThreeLap",
                    filter,
                    startDate ?? StartOf(2010, 1, 1),
                    endDate ?? EndOf(2030, 12, 31));

            return Assert.IsType<MkddHistoricalPlayerTimeline>(timeline);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await DatabaseTestSupport.DeleteDirectoryAsync(_directory);
        }
    }
}
