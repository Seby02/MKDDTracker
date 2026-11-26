using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalMultiPlayerComparisonServiceTests
{
    [Fact]
    public async Task CompareAsync_TwoPlayers_ComparesTimesAndRanks()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "multi-player-comparison.db");

        try
        {
            await using MkddDbContext db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            MkddCourse course =
                new()
                {
                    Name = "Luigi Circuit"
                };

            MkddPlayer alice =
                new()
                {
                    SitePlayerId = 1,
                    Name = "Alice",
                    Country = "FR"
                };

            MkddPlayer bob =
                new()
                {
                    SitePlayerId = 2,
                    Name = "Bob",
                    Country = "FR"
                };

            db.Courses.Add(course);
            db.Players.AddRange(
                alice,
                bob);

            await db.SaveChangesAsync();

            MkddHistoricalPlayerAlias aliceAlias =
                CreateAlias(
                    alice,
                    "Alice");

            MkddHistoricalPlayerAlias bobAlias =
                CreateAlias(
                    bob,
                    "Bob");

            db.HistoricalPlayerAliases.AddRange(
                aliceAlias,
                bobAlias);

            await db.SaveChangesAsync();

            //
            // 01/01 :
            //
            // Alice 1:15.000 => rang 1
            // Bob   1:16.000 => rang 2
            //

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    aliceAlias,
                    course,
                    "alice-start",
                    75_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1),

                CreatePerformance(
                    bobAlias,
                    course,
                    "bob-start",
                    76_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1));

            //
            // Bob améliore ensuite Alice.
            //
            // 10/01 :
            //
            // Bob   1:14.000 => rang 1
            // Alice 1:15.000 => rang 2
            //

            db.HistoricalPerformances.Add(
                CreatePerformance(
                    bobAlias,
                    course,
                    "bob-improvement",
                    74_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        10,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 2,
                    previousTime:
                        TimeSpan.FromMilliseconds(
                            76_000)));

            await db.SaveChangesAsync();

            var rankingService =
                new MkddHistoricalRankingService(db);

            var timelineService =
                new MkddHistoricalPlayerTimelineService(
                    db,
                    rankingService);

            var service =
                new MkddHistoricalMultiPlayerComparisonService(
                    rankingService,
                    timelineService);

            MkddHistoricalMultiPlayerComparison? result =
                await service.CompareAsync(
                    [alice.Id, bob.Id],
                    "Luigi Circuit",
                    "ThreeLap",
                    HistoricalFrequencyFilter.Unknown,
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        23,
                        59,
                        59,
                        TimeSpan.Zero),
                    new DateTimeOffset(
                        2026,
                        1,
                        10,
                        23,
                        59,
                        59,
                        TimeSpan.Zero));

            Assert.NotNull(result);

            Assert.Equal(
                2,
                result.Players.Count);

            MkddHistoricalPlayerComparisonEntry
                aliceResult =
                    Assert.Single(
                        result.Players,
                        player =>
                            player.PlayerId ==
                            alice.Id);

            Assert.Equal(
                TimeSpan.FromMilliseconds(
                    75_000),
                aliceResult.InitialTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(
                    75_000),
                aliceResult.FinalTime);

            Assert.Equal(
                TimeSpan.Zero,
                aliceResult.TimeImprovement);

            Assert.Equal(
                1,
                aliceResult.InitialRank);

            Assert.Equal(
                2,
                aliceResult.FinalRank);

            Assert.Equal(
                -1,
                aliceResult.PlacesGained);

            Assert.False(
                aliceResult.IsNewEntry);

            MkddHistoricalPlayerComparisonEntry
                bobResult =
                    Assert.Single(
                        result.Players,
                        player =>
                            player.PlayerId ==
                            bob.Id);

            Assert.Equal(
                TimeSpan.FromMilliseconds(
                    76_000),
                bobResult.InitialTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(
                    74_000),
                bobResult.FinalTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(
                    2_000),
                bobResult.TimeImprovement);

            Assert.Equal(
                2,
                bobResult.InitialRank);

            Assert.Equal(
                1,
                bobResult.FinalRank);

            Assert.Equal(
                1,
                bobResult.PlacesGained);

            Assert.False(
                bobResult.IsNewEntry);

            Assert.Equal(
                1,
                aliceResult.PrCount);

            Assert.Equal(
                2,
                bobResult.PrCount);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
    public async Task CompareAsync_MultipleAliasesForSamePlayer_AreConsolidated()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "multi-player-aliases.db");

        try
        {
            await using MkddDbContext db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            MkddCourse course =
                new()
                {
                    Name = "Luigi Circuit"
                };

            MkddPlayer player =
                new()
                {
                    SitePlayerId = 42,
                    Name = "Modern Player",
                    Country = "FR"
                };

            db.Courses.Add(course);
            db.Players.Add(player);

            await db.SaveChangesAsync();

            MkddHistoricalPlayerAlias firstAlias =
                CreateAlias(
                    player,
                    "Old Name");

            MkddHistoricalPlayerAlias secondAlias =
                CreateAlias(
                    player,
                    "Modern Player");

            db.HistoricalPlayerAliases.AddRange(
                firstAlias,
                secondAlias);

            await db.SaveChangesAsync();

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    firstAlias,
                    course,
                    "alias-first-pr",
                    76_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1),

                CreatePerformance(
                    secondAlias,
                    course,
                    "alias-second-pr",
                    75_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        5,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 2,
                    previousTime:
                        TimeSpan.FromMilliseconds(
                            76_000)));

            await db.SaveChangesAsync();

            var rankingService =
                new MkddHistoricalRankingService(db);

            var timelineService =
                new MkddHistoricalPlayerTimelineService(
                    db,
                    rankingService);

            var service =
                new MkddHistoricalMultiPlayerComparisonService(
                    rankingService,
                    timelineService);

            MkddHistoricalMultiPlayerComparison? result =
                await service.CompareAsync(
                    [player.Id],
                    "Luigi Circuit",
                    "ThreeLap",
                    HistoricalFrequencyFilter.Unknown,
                    new DateTimeOffset(
                        2025,
                        12,
                        31,
                        23,
                        59,
                        59,
                        TimeSpan.Zero),
                    new DateTimeOffset(
                        2026,
                        1,
                        10,
                        23,
                        59,
                        59,
                        TimeSpan.Zero));

            Assert.NotNull(result);

            MkddHistoricalPlayerComparisonEntry entry =
                Assert.Single(result.Players);

            Assert.Equal(
                player.Id,
                entry.PlayerId);

            Assert.Equal(
                "Modern Player",
                entry.PlayerName);

            Assert.Null(
                entry.InitialTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(75_000),
                entry.FinalTime);

            Assert.Equal(
                2,
                entry.PrCount);

            Assert.True(
                entry.IsNewEntry);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(directory);
        }
    }


    [Fact]
    public async Task CompareAsync_FrequencyFilters_AreIndependent()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "multi-player-frequencies.db");

        try
        {
            await using MkddDbContext db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            MkddCourse course =
                new()
                {
                    Name = "Luigi Circuit"
                };

            MkddPlayer player =
                new()
                {
                    SitePlayerId = 100,
                    Name = "Frequency Player",
                    Country = "FR"
                };

            db.Courses.Add(course);
            db.Players.Add(player);

            await db.SaveChangesAsync();

            MkddHistoricalPlayerAlias alias =
                CreateAlias(
                    player,
                    "Frequency Player");

            db.HistoricalPlayerAliases.Add(alias);

            await db.SaveChangesAsync();

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    alias,
                    course,
                    "frequency-unknown",
                    75_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        2,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1,
                    frequency: null),

                CreatePerformance(
                    alias,
                    course,
                    "frequency-50",
                    76_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        3,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1,
                    frequency: 50),

                CreatePerformance(
                    alias,
                    course,
                    "frequency-60",
                    74_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        4,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1,
                    frequency: 60));

            await db.SaveChangesAsync();

            var rankingService =
                new MkddHistoricalRankingService(db);

            var timelineService =
                new MkddHistoricalPlayerTimelineService(
                    db,
                    rankingService);

            var service =
                new MkddHistoricalMultiPlayerComparisonService(
                    rankingService,
                    timelineService);

            DateTimeOffset startDate =
                new(
                    2026,
                    1,
                    1,
                    23,
                    59,
                    59,
                    TimeSpan.Zero);

            DateTimeOffset endDate =
                new(
                    2026,
                    1,
                    10,
                    23,
                    59,
                    59,
                    TimeSpan.Zero);

            MkddHistoricalMultiPlayerComparison?
                unknownResult =
                    await service.CompareAsync(
                        [player.Id],
                        "Luigi Circuit",
                        "ThreeLap",
                        HistoricalFrequencyFilter.Unknown,
                        startDate,
                        endDate);

            MkddHistoricalMultiPlayerComparison?
                hz50Result =
                    await service.CompareAsync(
                        [player.Id],
                        "Luigi Circuit",
                        "ThreeLap",
                        HistoricalFrequencyFilter.Hz50,
                        startDate,
                        endDate);

            MkddHistoricalMultiPlayerComparison?
                hz60Result =
                    await service.CompareAsync(
                        [player.Id],
                        "Luigi Circuit",
                        "ThreeLap",
                        HistoricalFrequencyFilter.Hz60,
                        startDate,
                        endDate);

            Assert.NotNull(unknownResult);
            Assert.NotNull(hz50Result);
            Assert.NotNull(hz60Result);

            MkddHistoricalPlayerComparisonEntry unknown =
                Assert.Single(
                    unknownResult.Players);

            MkddHistoricalPlayerComparisonEntry hz50 =
                Assert.Single(
                    hz50Result.Players);

            MkddHistoricalPlayerComparisonEntry hz60 =
                Assert.Single(
                    hz60Result.Players);

            Assert.Equal(
                TimeSpan.FromMilliseconds(75_000),
                unknown.FinalTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(76_000),
                hz50.FinalTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(74_000),
                hz60.FinalTime);

            Assert.Equal(
                1,
                unknown.PrCount);

            Assert.Equal(
                1,
                hz50.PrCount);

            Assert.Equal(
                1,
                hz60.PrCount);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
        public async Task CompareAsync_NewPlayer_HasNoInitialState()
        {
            string directory =
                DatabaseTestSupport.CreateTemporaryDirectory();

            string databasePath =
                Path.Combine(
                    directory,
                    "multi-player-new-entry.db");

            try
            {
                await using MkddDbContext db =
                    DatabaseTestSupport.CreateContext(
                        databasePath);

                await db.Database.EnsureCreatedAsync();

                MkddCourse course =
                    new()
                    {
                        Name = "Luigi Circuit"
                    };

                MkddPlayer player =
                    new()
                    {
                        SitePlayerId = 10,
                        Name = "New Player",
                        Country = "FR"
                    };

                db.Courses.Add(course);
                db.Players.Add(player);

                await db.SaveChangesAsync();

                MkddHistoricalPlayerAlias alias =
                    CreateAlias(
                        player,
                        "New Player");

                db.HistoricalPlayerAliases.Add(alias);

                await db.SaveChangesAsync();

                db.HistoricalPerformances.Add(
                    CreatePerformance(
                        alias,
                        course,
                        "new-player",
                        74_500,
                        new DateTimeOffset(
                            2026,
                            1,
                            5,
                            12,
                            0,
                            0,
                            TimeSpan.Zero),
                        sequenceNumber: 1));

                await db.SaveChangesAsync();

                var rankingService =
                    new MkddHistoricalRankingService(db);

                var timelineService =
                    new MkddHistoricalPlayerTimelineService(
                        db,
                        rankingService);

                var service =
                    new MkddHistoricalMultiPlayerComparisonService(
                        rankingService,
                        timelineService);

                MkddHistoricalMultiPlayerComparison? result =
                    await service.CompareAsync(
                        [player.Id],
                        "Luigi Circuit",
                        "ThreeLap",
                        HistoricalFrequencyFilter.Unknown,
                        new DateTimeOffset(
                            2026,
                            1,
                            1,
                            23,
                            59,
                            59,
                            TimeSpan.Zero),
                        new DateTimeOffset(
                            2026,
                            1,
                            10,
                            23,
                            59,
                            59,
                            TimeSpan.Zero));

                Assert.NotNull(result);

                MkddHistoricalPlayerComparisonEntry entry =
                    Assert.Single(result.Players);

                Assert.Null(
                    entry.InitialTime);

                Assert.Null(
                    entry.InitialRank);

                Assert.Equal(
                    TimeSpan.FromMilliseconds(
                        74_500),
                    entry.FinalTime);

                Assert.Equal(
                    1,
                    entry.FinalRank);

                Assert.Equal(
                    1,
                    entry.PrCount);

                Assert.Null(
                    entry.TimeImprovement);

                Assert.Null(
                    entry.PlacesGained);

                Assert.True(
                    entry.IsNewEntry);

                Assert.True(
                    entry.HasData);
            }
            finally
            {
                await DatabaseTestSupport
                    .DeleteDirectoryAsync(
                        directory);
            }
        }

        [Fact]
        public async Task CompareAsync_DuplicatePlayerIds_ReturnsPlayerOnce()
        {
            string directory =
                DatabaseTestSupport.CreateTemporaryDirectory();

            string databasePath =
                Path.Combine(
                    directory,
                    "multi-player-duplicates.db");

            try
            {
                await using MkddDbContext db =
                    DatabaseTestSupport.CreateContext(
                        databasePath);

                await db.Database.EnsureCreatedAsync();

                MkddCourse course =
                    new()
                    {
                        Name = "Luigi Circuit"
                    };

                MkddPlayer player =
                    new()
                    {
                        SitePlayerId = 20,
                        Name = "Alice",
                        Country = "FR"
                    };

                db.Courses.Add(course);
                db.Players.Add(player);

                await db.SaveChangesAsync();

                MkddHistoricalPlayerAlias alias =
                    CreateAlias(
                        player,
                        "Alice");

                db.HistoricalPlayerAliases.Add(alias);

                await db.SaveChangesAsync();

                db.HistoricalPerformances.Add(
                    CreatePerformance(
                        alias,
                        course,
                        "alice",
                        75_000,
                        new DateTimeOffset(
                            2026,
                            1,
                            1,
                            12,
                            0,
                            0,
                            TimeSpan.Zero),
                        sequenceNumber: 1));

                await db.SaveChangesAsync();

                var rankingService =
                    new MkddHistoricalRankingService(db);

                var timelineService =
                    new MkddHistoricalPlayerTimelineService(
                        db,
                        rankingService);

                var service =
                    new MkddHistoricalMultiPlayerComparisonService(
                        rankingService,
                        timelineService);

                MkddHistoricalMultiPlayerComparison? result =
                    await service.CompareAsync(
                        [
                            player.Id,
                        player.Id,
                        player.Id
                        ],
                        "Luigi Circuit",
                        "ThreeLap",
                        HistoricalFrequencyFilter.Unknown,
                        new DateTimeOffset(
                            2026,
                            1,
                            1,
                            23,
                            59,
                            59,
                            TimeSpan.Zero),
                        new DateTimeOffset(
                            2026,
                            1,
                            2,
                            23,
                            59,
                            59,
                            TimeSpan.Zero));

                Assert.NotNull(result);

                Assert.Single(
                    result.Players);
            }
            finally
            {
                await DatabaseTestSupport
                    .DeleteDirectoryAsync(
                        directory);
            }
        }

        [Fact]
        public async Task CompareAsync_StartAfterEnd_Throws()
        {
            //
            // Cette validation intervient avant tout accès DB.
            // Une vraie base n'est donc pas nécessaire ici.
            //

            string directory =
                DatabaseTestSupport.CreateTemporaryDirectory();

            string databasePath =
                Path.Combine(
                    directory,
                    "multi-player-invalid-period.db");

            try
            {
                await using MkddDbContext db =
                    DatabaseTestSupport.CreateContext(
                        databasePath);

                var rankingService =
                    new MkddHistoricalRankingService(db);

                var timelineService =
                    new MkddHistoricalPlayerTimelineService(
                        db,
                        rankingService);

                var service =
                    new MkddHistoricalMultiPlayerComparisonService(
                        rankingService,
                        timelineService);

                await Assert.ThrowsAsync<ArgumentException>(
                    () =>
                        service.CompareAsync(
                            [1],
                            "Luigi Circuit",
                            "ThreeLap",
                            HistoricalFrequencyFilter.Unknown,
                            new DateTimeOffset(
                                2026,
                                2,
                                1,
                                0,
                                0,
                                0,
                                TimeSpan.Zero),
                            new DateTimeOffset(
                                2026,
                                1,
                                1,
                                0,
                                0,
                                0,
                                TimeSpan.Zero)));
            }
            finally
            {
                await DatabaseTestSupport
                    .DeleteDirectoryAsync(
                        directory);
            }
        }

    private static MkddHistoricalPlayerAlias CreateAlias(
        MkddPlayer player,
        string externalName) =>
        new()
        {
            SourceSystem =
                "Test",

            ExternalName =
                externalName,

            NormalizedExternalName =
                externalName.ToLowerInvariant(),

            Player =
                player,

            Country =
                player.Country,

            Confidence =
                100,

            ResolutionStatus =
                "Matched"
        };

    private static MkddHistoricalPerformance CreatePerformance(
        MkddHistoricalPlayerAlias alias,
        MkddCourse course,
        string externalKey,
        long milliseconds,
        DateTimeOffset firstSeenAt,
        int sequenceNumber,
        TimeSpan? previousTime = null,
        int? frequency = null,
        string timeType = "ThreeLap") =>
        new()
        {
            ExternalKey =
                externalKey,

            HistoricalPlayerAlias =
                alias,

            Course =
                course,

            TimeType =
                timeType,

            Frequency =
                frequency,

            Time =
                TimeSpan.FromMilliseconds(
                    milliseconds),

            FirstSeenAt =
                firstSeenAt,

            DateSource =
                "Test",

            SequenceNumber =
                sequenceNumber,

            PreviousTime =
                previousTime,

            Improvement =
                previousTime.HasValue
                    ? previousTime.Value -
                      TimeSpan.FromMilliseconds(
                          milliseconds)
                    : null,

            OccurrenceCount =
                1,

            SourceSystem =
                "Test",

            ImportStatus =
                "Approved"
        };

    [Fact]
    public async Task CompareAsync_TimeTypes_AreIndependent()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "multi-player-time-types.db");

        try
        {
            await using MkddDbContext db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            MkddCourse course =
                new()
                {
                    Name = "Luigi Circuit"
                };

            MkddPlayer player =
                new()
                {
                    SitePlayerId = 200,
                    Name = "Time Type Player",
                    Country = "FR"
                };

            db.Courses.Add(course);
            db.Players.Add(player);

            await db.SaveChangesAsync();

            MkddHistoricalPlayerAlias alias =
                CreateAlias(
                    player,
                    "Time Type Player");

            db.HistoricalPlayerAliases.Add(alias);

            await db.SaveChangesAsync();

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    alias,
                    course,
                    "three-lap-performance",
                    75_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        2,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1,
                    timeType: "ThreeLap"),

                CreatePerformance(
                    alias,
                    course,
                    "flap-performance",
                    24_500,
                    new DateTimeOffset(
                        2026,
                        1,
                        3,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1,
                    timeType: "Flap"));

            await db.SaveChangesAsync();

            var rankingService =
                new MkddHistoricalRankingService(db);

            var timelineService =
                new MkddHistoricalPlayerTimelineService(
                    db,
                    rankingService);

            var service =
                new MkddHistoricalMultiPlayerComparisonService(
                    rankingService,
                    timelineService);

            DateTimeOffset startDate =
                new(
                    2026,
                    1,
                    1,
                    23,
                    59,
                    59,
                    TimeSpan.Zero);

            DateTimeOffset endDate =
                new(
                    2026,
                    1,
                    10,
                    23,
                    59,
                    59,
                    TimeSpan.Zero);

            MkddHistoricalMultiPlayerComparison? threeLapResult =
                await service.CompareAsync(
                    [player.Id],
                    "Luigi Circuit",
                    "ThreeLap",
                    HistoricalFrequencyFilter.Unknown,
                    startDate,
                    endDate);

            MkddHistoricalMultiPlayerComparison? flapResult =
                await service.CompareAsync(
                    [player.Id],
                    "Luigi Circuit",
                    "Flap",
                    HistoricalFrequencyFilter.Unknown,
                    startDate,
                    endDate);

            Assert.NotNull(threeLapResult);
            Assert.NotNull(flapResult);

            MkddHistoricalPlayerComparisonEntry threeLap =
                Assert.Single(
                    threeLapResult.Players);

            MkddHistoricalPlayerComparisonEntry flap =
                Assert.Single(
                    flapResult.Players);

            Assert.Equal(
                "ThreeLap",
                threeLapResult.TimeType);

            Assert.Equal(
                "Flap",
                flapResult.TimeType);

            Assert.Equal(
                TimeSpan.FromMilliseconds(75_000),
                threeLap.FinalTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(24_500),
                flap.FinalTime);

            Assert.Equal(
                1,
                threeLap.PrCount);

            Assert.Equal(
                1,
                flap.PrCount);

            Assert.NotEqual(
                threeLap.FinalTime,
                flap.FinalTime);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
    public async Task CompareAsync_TiedTimes_PreserveCompetitionRanking()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "multi-player-ties.db");

        try
        {
            await using MkddDbContext db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            MkddCourse course =
                new()
                {
                    Name = "Luigi Circuit"
                };

            MkddPlayer alice =
                new()
                {
                    SitePlayerId = 1,
                    Name = "Alice",
                    Country = "FR"
                };

            MkddPlayer bob =
                new()
                {
                    SitePlayerId = 2,
                    Name = "Bob",
                    Country = "FR"
                };

            MkddPlayer claire =
                new()
                {
                    SitePlayerId = 3,
                    Name = "Claire",
                    Country = "FR"
                };

            MkddPlayer david =
                new()
                {
                    SitePlayerId = 4,
                    Name = "David",
                    Country = "FR"
                };

            db.Courses.Add(course);

            db.Players.AddRange(
                alice,
                bob,
                claire,
                david);

            await db.SaveChangesAsync();

            MkddHistoricalPlayerAlias aliceAlias =
                CreateAlias(alice, "Alice");

            MkddHistoricalPlayerAlias bobAlias =
                CreateAlias(bob, "Bob");

            MkddHistoricalPlayerAlias claireAlias =
                CreateAlias(claire, "Claire");

            MkddHistoricalPlayerAlias davidAlias =
                CreateAlias(david, "David");

            db.HistoricalPlayerAliases.AddRange(
                aliceAlias,
                bobAlias,
                claireAlias,
                davidAlias);

            await db.SaveChangesAsync();

            DateTimeOffset rankingDate =
                new(
                    2026,
                    1,
                    5,
                    12,
                    0,
                    0,
                    TimeSpan.Zero);

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    aliceAlias,
                    course,
                    "alice-tie-test",
                    74_000,
                    rankingDate,
                    sequenceNumber: 1),

                CreatePerformance(
                    bobAlias,
                    course,
                    "bob-tie-test",
                    75_000,
                    rankingDate,
                    sequenceNumber: 1),

                CreatePerformance(
                    claireAlias,
                    course,
                    "claire-tie-test",
                    75_000,
                    rankingDate,
                    sequenceNumber: 1),

                CreatePerformance(
                    davidAlias,
                    course,
                    "david-tie-test",
                    76_000,
                    rankingDate,
                    sequenceNumber: 1));

            await db.SaveChangesAsync();

            var rankingService =
                new MkddHistoricalRankingService(db);

            var timelineService =
                new MkddHistoricalPlayerTimelineService(
                    db,
                    rankingService);

            var service =
                new MkddHistoricalMultiPlayerComparisonService(
                    rankingService,
                    timelineService);

            MkddHistoricalMultiPlayerComparison? result =
                await service.CompareAsync(
                    [
                        alice.Id,
                    bob.Id,
                    claire.Id,
                    david.Id
                    ],
                    "Luigi Circuit",
                    "ThreeLap",
                    HistoricalFrequencyFilter.Unknown,
                    new DateTimeOffset(
                        2026,
                        1,
                        5,
                        23,
                        59,
                        59,
                        TimeSpan.Zero),
                    new DateTimeOffset(
                        2026,
                        1,
                        10,
                        23,
                        59,
                        59,
                        TimeSpan.Zero));

            Assert.NotNull(result);

            Assert.Equal(
                4,
                result.Players.Count);

            MkddHistoricalPlayerComparisonEntry aliceResult =
                Assert.Single(
                    result.Players,
                    player => player.PlayerId == alice.Id);

            MkddHistoricalPlayerComparisonEntry bobResult =
                Assert.Single(
                    result.Players,
                    player => player.PlayerId == bob.Id);

            MkddHistoricalPlayerComparisonEntry claireResult =
                Assert.Single(
                    result.Players,
                    player => player.PlayerId == claire.Id);

            MkddHistoricalPlayerComparisonEntry davidResult =
                Assert.Single(
                    result.Players,
                    player => player.PlayerId == david.Id);

            Assert.Equal(1, aliceResult.InitialRank);
            Assert.Equal(1, aliceResult.FinalRank);

            Assert.Equal(2, bobResult.InitialRank);
            Assert.Equal(2, bobResult.FinalRank);

            Assert.Equal(2, claireResult.InitialRank);
            Assert.Equal(2, claireResult.FinalRank);

            Assert.Equal(4, davidResult.InitialRank);
            Assert.Equal(4, davidResult.FinalRank);

            Assert.Equal(0, aliceResult.PlacesGained);
            Assert.Equal(0, bobResult.PlacesGained);
            Assert.Equal(0, claireResult.PlacesGained);
            Assert.Equal(0, davidResult.PlacesGained);

            Assert.Equal(1, aliceResult.PrCount);
            Assert.Equal(1, bobResult.PrCount);
            Assert.Equal(1, claireResult.PrCount);
            Assert.Equal(1, davidResult.PrCount);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
    public async Task CompareAsync_NoNewPr_CanStillLoseRank()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "multi-player-rank-loss-without-pr.db");

        try
        {
            await using MkddDbContext db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            MkddCourse course =
                new()
                {
                    Name = "Luigi Circuit"
                };

            MkddPlayer alice =
                new()
                {
                    SitePlayerId = 1,
                    Name = "Alice",
                    Country = "FR"
                };

            MkddPlayer bob =
                new()
                {
                    SitePlayerId = 2,
                    Name = "Bob",
                    Country = "FR"
                };

            db.Courses.Add(course);
            db.Players.AddRange(
                alice,
                bob);

            await db.SaveChangesAsync();

            MkddHistoricalPlayerAlias aliceAlias =
                CreateAlias(
                    alice,
                    "Alice");

            MkddHistoricalPlayerAlias bobAlias =
                CreateAlias(
                    bob,
                    "Bob");

            db.HistoricalPlayerAliases.AddRange(
                aliceAlias,
                bobAlias);

            await db.SaveChangesAsync();

            //
            // État avant la période :
            //
            // Alice : 75.000 -> rang 1
            // Bob   : 76.000 -> rang 2
            //
            // Ces PR datent du 01/01.
            //

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    aliceAlias,
                    course,
                    "alice-before-period",
                    75_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1),

                CreatePerformance(
                    bobAlias,
                    course,
                    "bob-before-period",
                    76_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1));

            //
            // Bob améliore pendant la période.
            //
            // Alice ne réalise aucun nouveau PR.
            //
            // Fin :
            //
            // Bob   : 74.000 -> rang 1
            // Alice : 75.000 -> rang 2
            //

            db.HistoricalPerformances.Add(
                CreatePerformance(
                    bobAlias,
                    course,
                    "bob-during-period",
                    74_000,
                    new DateTimeOffset(
                        2026,
                        1,
                        10,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 2,
                    previousTime:
                        TimeSpan.FromMilliseconds(
                            76_000)));

            await db.SaveChangesAsync();

            var rankingService =
                new MkddHistoricalRankingService(db);

            var timelineService =
                new MkddHistoricalPlayerTimelineService(
                    db,
                    rankingService);

            var service =
                new MkddHistoricalMultiPlayerComparisonService(
                    rankingService,
                    timelineService);

            //
            // La période commence après les premiers PR.
            //
            // Donc Alice doit avoir :
            //
            // PrCount = 0
            //
            // tout en conservant son chrono initial de 75.000
            // dans le classement au début de période.
            //

            MkddHistoricalMultiPlayerComparison? result =
                await service.CompareAsync(
                    [
                        alice.Id,
                    bob.Id
                    ],
                    "Luigi Circuit",
                    "ThreeLap",
                    HistoricalFrequencyFilter.Unknown,
                    new DateTimeOffset(
                        2026,
                        1,
                        5,
                        23,
                        59,
                        59,
                        TimeSpan.Zero),
                    new DateTimeOffset(
                        2026,
                        1,
                        15,
                        23,
                        59,
                        59,
                        TimeSpan.Zero));

            Assert.NotNull(result);

            MkddHistoricalPlayerComparisonEntry aliceResult =
                Assert.Single(
                    result.Players,
                    player =>
                        player.PlayerId ==
                        alice.Id);

            MkddHistoricalPlayerComparisonEntry bobResult =
                Assert.Single(
                    result.Players,
                    player =>
                        player.PlayerId ==
                        bob.Id);

            //
            // Alice n'améliore jamais son chrono.
            //

            Assert.Equal(
                TimeSpan.FromMilliseconds(75_000),
                aliceResult.InitialTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(75_000),
                aliceResult.FinalTime);

            Assert.Equal(
                TimeSpan.Zero,
                aliceResult.TimeImprovement);

            Assert.Equal(
                1,
                aliceResult.InitialRank);

            Assert.Equal(
                2,
                aliceResult.FinalRank);

            Assert.Equal(
                -1,
                aliceResult.PlacesGained);

            Assert.Equal(
                0,
                aliceResult.PrCount);

            Assert.False(
                aliceResult.IsNewEntry);

            //
            // Bob réalise le seul PR de la période.
            //

            Assert.Equal(
                TimeSpan.FromMilliseconds(76_000),
                bobResult.InitialTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(74_000),
                bobResult.FinalTime);

            Assert.Equal(
                TimeSpan.FromMilliseconds(2_000),
                bobResult.TimeImprovement);

            Assert.Equal(
                2,
                bobResult.InitialRank);

            Assert.Equal(
                1,
                bobResult.FinalRank);

            Assert.Equal(
                1,
                bobResult.PlacesGained);

            Assert.Equal(
                1,
                bobResult.PrCount);
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }
}