using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalRankingServiceTests
{
    [Fact]
    public async Task GetRankingAtAsync_UsesLatestKnownPrForEachPlayer()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "historical-ranking.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            var course =
                new MkddCourse
                {
                    Name = "Luigi Circuit"
                };

            var playerOne =
                new MkddHistoricalPlayerAlias
                {
                    SourceSystem = "YahooGroups",
                    ExternalName = "Player One",
                    NormalizedExternalName = "player one",
                    Confidence = 100,
                    ResolutionStatus = "Manual"
                };

            var playerTwo =
                new MkddHistoricalPlayerAlias
                {
                    SourceSystem = "YahooGroups",
                    ExternalName = "Player Two",
                    NormalizedExternalName = "player two",
                    Confidence = 100,
                    ResolutionStatus = "Manual"
                };

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    "one-first",
                    playerOne,
                    course,
                    90_000,
                    new DateTimeOffset(
                        2004,
                        1,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1),

                CreatePerformance(
                    "one-second",
                    playerOne,
                    course,
                    85_000,
                    new DateTimeOffset(
                        2004,
                        2,
                        1,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 2),

                CreatePerformance(
                    "two-first",
                    playerTwo,
                    course,
                    87_000,
                    new DateTimeOffset(
                        2004,
                        1,
                        15,
                        12,
                        0,
                        0,
                        TimeSpan.Zero),
                    sequenceNumber: 1));

            await db.SaveChangesAsync();

            var service =
                new MkddHistoricalRankingService(db);

            MkddHistoricalRanking? result =
                await service.GetRankingAtAsync(
                    "Luigi Circuit",
                    "ThreeLap",
                    HistoricalFrequencyFilter.Hz60,
                    new DateTimeOffset(
                        2004,
                        2,
                        15,
                        23,
                        59,
                        59,
                        TimeSpan.Zero));

            Assert.NotNull(result);

            Assert.Collection(
                result.Entries,

                first =>
                {
                    Assert.Equal(
                        "Player One",
                        first.PlayerName);

                    Assert.Equal(
                        1,
                        first.Rank);

                    Assert.Equal(
                        TimeSpan.FromMilliseconds(85_000),
                        first.Time);
                },

                second =>
                {
                    Assert.Equal(
                        "Player Two",
                        second.PlayerName);

                    Assert.Equal(
                        2,
                        second.Rank);

                    Assert.Equal(
                        TimeSpan.FromMilliseconds(87_000),
                        second.Time);
                });
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    [Fact]
    public async Task GetRankingAtAsync_AssignsSameRankToEqualTimes()
    {
        string directory =
            DatabaseTestSupport.CreateTemporaryDirectory();

        string databasePath =
            Path.Combine(
                directory,
                "historical-ranking-ties.db");

        try
        {
            await using var db =
                DatabaseTestSupport.CreateContext(
                    databasePath);

            await db.Database.EnsureCreatedAsync();

            var course =
                new MkddCourse
                {
                    Name = "Mario Circuit"
                };

            var playerOne =
                CreateAlias(
                    "Player One");

            var playerTwo =
                CreateAlias(
                    "Player Two");

            var playerThree =
                CreateAlias(
                    "Player Three");

            DateTimeOffset date =
                new(
                    2004,
                    1,
                    1,
                    12,
                    0,
                    0,
                    TimeSpan.Zero);

            db.HistoricalPerformances.AddRange(
                CreatePerformance(
                    "tie-one",
                    playerOne,
                    course,
                    80_000,
                    date,
                    1),

                CreatePerformance(
                    "tie-two",
                    playerTwo,
                    course,
                    80_000,
                    date,
                    1),

                CreatePerformance(
                    "tie-three",
                    playerThree,
                    course,
                    82_000,
                    date,
                    1));

            await db.SaveChangesAsync();

            var service =
                new MkddHistoricalRankingService(db);

            MkddHistoricalRanking? result =
                await service.GetRankingAtAsync(
                    "Mario Circuit",
                    "ThreeLap",
                    HistoricalFrequencyFilter.Hz60,
                    date.AddDays(1));

            Assert.NotNull(result);

            Assert.Equal(
                new[] { 1, 1, 3 },
                result.Entries
                    .Select(entry => entry.Rank)
                    .ToArray());
        }
        finally
        {
            await DatabaseTestSupport
                .DeleteDirectoryAsync(
                    directory);
        }
    }

    private static MkddHistoricalPlayerAlias CreateAlias(
        string name)
    {
        return new MkddHistoricalPlayerAlias
        {
            SourceSystem = "YahooGroups",
            ExternalName = name,
            NormalizedExternalName =
                name.ToLowerInvariant(),
            Confidence = 100,
            ResolutionStatus = "Manual"
        };
    }

    private static MkddHistoricalPerformance
        CreatePerformance(
            string externalKey,
            MkddHistoricalPlayerAlias alias,
            MkddCourse course,
            long milliseconds,
            DateTimeOffset firstSeenAt,
            int sequenceNumber)
    {
        return new MkddHistoricalPerformance
        {
            ExternalKey =
                externalKey,

            HistoricalPlayerAlias =
                alias,

            Course =
                course,

            TimeType =
                "ThreeLap",

            Frequency =
                60,

            Time =
                TimeSpan.FromMilliseconds(
                    milliseconds),

            FirstSeenAt =
                firstSeenAt,

            DateSource =
                "YahooFirstSeenDate",

            SequenceNumber =
                sequenceNumber,

            OccurrenceCount =
                1,

            SourceSystem =
                "YahooGroups",

            ImportStatus =
                "Approved"
        };
    }
}