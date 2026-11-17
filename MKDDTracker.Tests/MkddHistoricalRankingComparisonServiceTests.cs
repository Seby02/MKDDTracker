using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalRankingComparisonServiceTests
{
    [Fact]
    public async Task CompareAsync_CalculatesRankAndTimeImprovement()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "comparison.db");

        try
        {
            await using var db = DatabaseTestSupport.CreateContext(databasePath);
            await db.Database.EnsureCreatedAsync();

            var course = new MkddCourse { Name = "Luigi Circuit" };
            var playerOne = CreateAlias("Player One");
            var playerTwo = CreateAlias("Player Two");
            var newPlayer = CreateAlias("New Player");
            DateTimeOffset initialDate = new(2004, 1, 1, 12, 0, 0, TimeSpan.Zero);
            DateTimeOffset finalDate = initialDate.AddDays(1);

            db.HistoricalPerformances.AddRange(
                CreatePerformance("one-initial", playerOne, course, 100_000, initialDate),
                CreatePerformance("two-initial", playerTwo, course, 95_000, initialDate),
                CreatePerformance("one-final", playerOne, course, 95_000, finalDate, 2),
                CreatePerformance("two-final", playerTwo, course, 100_000, finalDate, 2),
                CreatePerformance("new-final", newPlayer, course, 110_000, finalDate));
            await db.SaveChangesAsync();

            var rankingService = new MkddHistoricalRankingService(db);
            var service = new MkddHistoricalRankingComparisonService(rankingService);

            MkddHistoricalRankingComparison result = await service.CompareAsync(
                "Luigi Circuit", "ThreeLap", HistoricalFrequencyFilter.Hz60,
                initialDate, finalDate);

            MkddHistoricalRankingComparisonEntry player = Assert.Single(
                result.Entries,
                entry => entry.PlayerName == "Player One");
            Assert.Equal(MkddHistoricalRankingComparisonStatus.Compared, player.Status);
            Assert.Equal(2, player.InitialRank);
            Assert.Equal(1, player.FinalRank);
            Assert.Equal(TimeSpan.FromSeconds(100), player.InitialTime);
            Assert.Equal(TimeSpan.FromSeconds(95), player.FinalTime);
            Assert.Equal(1, player.PlacesGained);
            Assert.Equal(TimeSpan.FromSeconds(5), player.TimeImprovement);
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    [Fact]
    public async Task CompareAsync_IdentifiesNewEntrant()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string databasePath = Path.Combine(directory, "new-entrant.db");

        try
        {
            await using var db = DatabaseTestSupport.CreateContext(databasePath);
            await db.Database.EnsureCreatedAsync();
            var course = new MkddCourse { Name = "Luigi Circuit" };
            var existing = CreateAlias("Existing Player");
            var newPlayer = CreateAlias("New Player");
            DateTimeOffset initialDate = new(2004, 1, 1, 12, 0, 0, TimeSpan.Zero);
            DateTimeOffset finalDate = initialDate.AddDays(1);

            db.HistoricalPerformances.AddRange(
                CreatePerformance("existing", existing, course, 100_000, initialDate),
                CreatePerformance("new", newPlayer, course, 110_000, finalDate));
            await db.SaveChangesAsync();

            var service = new MkddHistoricalRankingComparisonService(
                new MkddHistoricalRankingService(db));
            MkddHistoricalRankingComparison result = await service.CompareAsync(
                "Luigi Circuit", "ThreeLap", HistoricalFrequencyFilter.Hz60,
                initialDate, finalDate);

            MkddHistoricalRankingComparisonEntry entry = Assert.Single(
                result.Entries,
                item => item.PlayerName == "New Player");
            Assert.Equal(MkddHistoricalRankingComparisonStatus.NewEntrant, entry.Status);
            Assert.Null(entry.InitialRank);
            Assert.Equal(2, entry.FinalRank);
            Assert.Null(entry.PlacesGained);
            Assert.Null(entry.TimeImprovement);
        }
        finally
        {
            await DatabaseTestSupport.DeleteDirectoryAsync(directory);
        }
    }

    private static MkddHistoricalPlayerAlias CreateAlias(string name) => new()
    {
        SourceSystem = "YahooGroups",
        ExternalName = name,
        NormalizedExternalName = name.ToLowerInvariant(),
        Confidence = 100,
        ResolutionStatus = "Manual"
    };

    private static MkddHistoricalPerformance CreatePerformance(
        string key, MkddHistoricalPlayerAlias alias, MkddCourse course,
        long milliseconds, DateTimeOffset firstSeenAt, int sequenceNumber = 1) => new()
    {
        ExternalKey = key,
        HistoricalPlayerAlias = alias,
        Course = course,
        TimeType = "ThreeLap",
        Frequency = 60,
        Time = TimeSpan.FromMilliseconds(milliseconds),
        FirstSeenAt = firstSeenAt,
        DateSource = "YahooFirstSeenDate",
        SequenceNumber = sequenceNumber,
        OccurrenceCount = 1,
        SourceSystem = "YahooGroups",
        ImportStatus = "Approved"
    };
}
