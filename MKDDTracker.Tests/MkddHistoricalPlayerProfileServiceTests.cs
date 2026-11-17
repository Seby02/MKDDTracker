using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddHistoricalPlayerProfileServiceTests
{
    [Fact]
    public async Task GetProfileAsync_MergesLinkedAliasesAndConsolidatesCrossSourceEvents()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string path = Path.Combine(directory, "profile-merged.db");
        try
        {
            var player = CreateModernPlayer("Modern Racer");
            var yahooAliasOne = CreateAlias("Old Racer", player, "YahooGroups");
            var yahooAliasTwo = CreateAlias("Yahoo Racer", player, "YahooGroups");
            var modernAlias = CreateAlias("Modern Racer", player, "MKDDTracker");
            var opponent = CreateAlias("Opponent", null, "YahooGroups");
            var course = new MkddCourse { Name = "Luigi Circuit" };
            DateTimeOffset syntheticFirstDate = Utc(2006, 6, 9);
            DateTimeOffset secondDate = Utc(2006, 6, 10);
            using var requests = new RankingRequestCollector();

            await using (var db = DatabaseTestSupport.CreateContext(path))
            {
                await db.Database.EnsureCreatedAsync();
                db.HistoricalPerformances.AddRange(
                    CreatePerformance("yahoo-copy", yahooAliasOne, course, "ThreeLap", 60, 100_000, syntheticFirstDate, "YahooGroups", "SyntheticPre2003Correction"),
                    CreatePerformance("modern-copy", modernAlias, course, "ThreeLap", null, 100_000, syntheticFirstDate, "MKDDTracker", "MKDDTrackerRecordDate"),
                    CreatePerformance("second-pr", yahooAliasTwo, course, "ThreeLap", 60, 85_000, secondDate, "YahooGroups"),
                    CreatePerformance("opponent-pr", opponent, course, "ThreeLap", 60, 90_000, secondDate, "YahooGroups"));
                await db.SaveChangesAsync();
            }

            await using var verify = DatabaseTestSupport.CreateContext(path);
            var option = new HistoricalPlayerOption
            {
                Key = $"player:{player.Id}",
                DisplayName = player.Name,
                PlayerId = player.Id,
                HistoricalPlayerAliasId = yahooAliasOne.Id
            };
            var service = new MkddHistoricalPlayerProfileService(
                CreateFactory(path),
                requests.Add);
            MkddHistoricalPlayerProfile? profile = await service.GetProfileAsync(option);

            Assert.NotNull(profile);
            Assert.Equal(player.Id, profile.PlayerId);
            Assert.True(profile.IsLinkedToModernPlayer);
            Assert.Equal("Modern Racer", profile.DisplayName);
            Assert.Equal("FR", profile.Country);
            Assert.Equal(new[] { "Modern Racer", "Old Racer", "Yahoo Racer" }, profile.AliasNames);
            Assert.Equal(1, profile.DistinctCoursesCount);
            Assert.Equal(2, profile.TotalHistoricalEvents);
            Assert.Equal(syntheticFirstDate, profile.FirstKnownPerformanceAt);
            Assert.Equal(secondDate, profile.LastKnownPerformanceAt);

            var category = Assert.Single(profile.Categories);
            Assert.Equal("ThreeLap", category.TimeType);
            Assert.Equal(HistoricalFrequencyFilter.Hz60, category.FrequencyFilter);
            Assert.Equal(TimeSpan.FromMilliseconds(100_000), category.FirstKnownTime);
            Assert.Equal(TimeSpan.FromMilliseconds(85_000), category.CurrentBestTime);
            Assert.Equal(TimeSpan.FromMilliseconds(15_000), category.Improvement);
            Assert.Equal(syntheticFirstDate, category.FirstKnownAt);
            Assert.Equal(secondDate, category.CurrentBestAt);
            Assert.Equal(secondDate, category.LastKnownAt);
            Assert.Equal(2, category.HistoricalEventCount);
            Assert.Equal(1, category.CurrentRank);
            Assert.Equal(2, category.RankedPlayerCount);
            Assert.Equal(new[] { "MKDDTracker", "YahooGroups" }, category.Sources);
            Assert.Equal("SyntheticPre2003Correction", (await verify.HistoricalPerformances
                .AsNoTracking()
                .SingleAsync(performance => performance.ExternalKey == "yahoo-copy")).DateSource);
            Assert.Single(requests.Requests);
            Assert.Equal(new DateOnly(2006, 6, 10), requests.Requests[0].Date);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetProfileAsync_KeepsUnlinkedSameNameAliasesSeparateAndReturnsNullForUnknownIdentity()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string path = Path.Combine(directory, "profile-unlinked.db");
        try
        {
            var firstAlias = CreateAlias("Same Name", null, "YahooGroups");
            var secondAlias = CreateAlias("Same Name", null, "YahooGroups");
            var course = new MkddCourse { Name = "Mario Circuit" };
            await using (var db = DatabaseTestSupport.CreateContext(path))
            {
                await db.Database.EnsureCreatedAsync();
                db.HistoricalPerformances.AddRange(
                    CreatePerformance("first", firstAlias, course, "ThreeLap", 50, 90_000, Utc(2010, 1, 1)),
                    CreatePerformance("second", secondAlias, course, "ThreeLap", 50, 95_000, Utc(2010, 1, 1)));
                await db.SaveChangesAsync();
            }

            var service = new MkddHistoricalPlayerProfileService(CreateFactory(path));
            var firstProfile = await service.GetProfileAsync(Option(firstAlias));
            var secondProfile = await service.GetProfileAsync(Option(secondAlias));

            Assert.NotNull(firstProfile);
            Assert.NotNull(secondProfile);
            Assert.Equal(firstProfile.DisplayName, secondProfile.DisplayName);
            Assert.NotEqual(firstProfile.PlayerKey, secondProfile.PlayerKey);
            Assert.Null(firstProfile.PlayerId);
            Assert.Null(firstProfile.Country);
            Assert.False(firstProfile.IsLinkedToModernPlayer);
            Assert.Equal($"alias:{firstAlias.Id}", firstProfile.PlayerKey);
            Assert.Null(await service.GetProfileAsync(new HistoricalPlayerOption
            {
                Key = "alias:999999",
                DisplayName = "Missing",
                HistoricalPlayerAliasId = 999999
            }));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetProfileAsync_SeparatesTypesAndAllThreeFrequencyCategoriesAndCachesCategoryRankings()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string path = Path.Combine(directory, "profile-categories.db");
        try
        {
            var alias = CreateAlias("Categories", null, "YahooGroups");
            var course = new MkddCourse { Name = "Peach Circuit" };
            int sequence = 0;
            var performances = new List<MkddHistoricalPerformance>();
            foreach (string type in new[] { "ThreeLap", "Flap" })
            {
                foreach (int? frequency in new int?[] { 50, 60, null })
                {
                    performances.Add(CreatePerformance(
                        $"{type}-{frequency}-first", alias, course, type, frequency,
                        100_000 + sequence, Utc(2011, 1, 1).AddDays(sequence), "YahooGroups"));
                    sequence++;
                    performances.Add(CreatePerformance(
                        $"{type}-{frequency}-second", alias, course, type, frequency,
                        90_000 + sequence, Utc(2011, 1, 1).AddDays(sequence), "YahooGroups"));
                    sequence++;
                }
            }

            await using (var db = DatabaseTestSupport.CreateContext(path))
            {
                await db.Database.EnsureCreatedAsync();
                db.HistoricalPerformances.AddRange(performances);
                await db.SaveChangesAsync();
            }

            using var requests = new RankingRequestCollector();
            var service = new MkddHistoricalPlayerProfileService(CreateFactory(path), requests.Add);
            MkddHistoricalPlayerProfile? profile = await service.GetProfileAsync(Option(alias));

            Assert.NotNull(profile);
            Assert.Equal(6, profile.Categories.Count);
            Assert.Equal(6, profile.Categories.Select(category => (category.TimeType, category.FrequencyFilter)).Distinct().Count());
            Assert.Contains(profile.Categories, category => category.TimeType == "ThreeLap" && category.FrequencyFilter == HistoricalFrequencyFilter.Hz50);
            Assert.Contains(profile.Categories, category => category.TimeType == "ThreeLap" && category.FrequencyFilter == HistoricalFrequencyFilter.Hz60);
            Assert.Contains(profile.Categories, category => category.TimeType == "ThreeLap" && category.FrequencyFilter == HistoricalFrequencyFilter.Unknown);
            Assert.Contains(profile.Categories, category => category.TimeType == "Flap" && category.FrequencyFilter == HistoricalFrequencyFilter.Hz50);
            Assert.Contains(profile.Categories, category => category.TimeType == "Flap" && category.FrequencyFilter == HistoricalFrequencyFilter.Hz60);
            Assert.Contains(profile.Categories, category => category.TimeType == "Flap" && category.FrequencyFilter == HistoricalFrequencyFilter.Unknown);
            Assert.All(profile.Categories, category => Assert.Equal(2, category.HistoricalEventCount));
            Assert.Equal(6, requests.Requests.Count);
            Assert.Equal(1, requests.Requests.Count(request =>
                request.CourseName == "Peach Circuit" &&
                request.TimeType == "ThreeLap" &&
                request.Frequency == HistoricalFrequencyFilter.Hz50));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetProfileAsync_DoesNotMergeSameTimeObservationsAcrossFrequencies()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string path = Path.Combine(directory, "profile-frequency-collision.db");
        try
        {
            var alias = CreateAlias("Frequency Player", null, "YahooGroups");
            var course = new MkddCourse { Name = "Yoshi Circuit" };
            DateTimeOffset date = Utc(2012, 3, 4);
            await using (var db = DatabaseTestSupport.CreateContext(path))
            {
                await db.Database.EnsureCreatedAsync();
                db.HistoricalPerformances.AddRange(
                    CreatePerformance("50hz", alias, course, "ThreeLap", 50, 90_000, date),
                    CreatePerformance("60hz", alias, course, "ThreeLap", 60, 90_000, date),
                    CreatePerformance("unknown-hz", alias, course, "ThreeLap", null, 90_000, date));
                await db.SaveChangesAsync();
            }

            MkddHistoricalPlayerProfile? profile = await new MkddHistoricalPlayerProfileService(CreateFactory(path))
                .GetProfileAsync(Option(alias));

            Assert.NotNull(profile);
            Assert.Equal(3, profile.Categories.Count);
            Assert.Contains(profile.Categories, category => category.FrequencyFilter == HistoricalFrequencyFilter.Hz50);
            Assert.Contains(profile.Categories, category => category.FrequencyFilter == HistoricalFrequencyFilter.Hz60);
            Assert.Contains(profile.Categories, category => category.FrequencyFilter == HistoricalFrequencyFilter.Unknown);
            Assert.All(profile.Categories, category => Assert.Equal(1, category.HistoricalEventCount));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsLinkedAliasWithNoPerformances()
    {
        string directory = DatabaseTestSupport.CreateTemporaryDirectory();
        string path = Path.Combine(directory, "profile-empty.db");
        try
        {
            var modern = CreateModernPlayer("Empty Profile");
            var alias = CreateAlias("Old Empty", modern, "YahooGroups");
            await using (var db = DatabaseTestSupport.CreateContext(path))
            {
                await db.Database.EnsureCreatedAsync();
                db.HistoricalPlayerAliases.Add(alias);
                await db.SaveChangesAsync();
            }

            var profile = await new MkddHistoricalPlayerProfileService(CreateFactory(path))
                .GetProfileAsync(new HistoricalPlayerOption
                {
                    Key = $"player:{modern.Id}",
                    DisplayName = modern.Name,
                    PlayerId = modern.Id,
                    HistoricalPlayerAliasId = alias.Id
                });

            Assert.NotNull(profile);
            Assert.Empty(profile.Categories);
            Assert.Equal(0, profile.TotalHistoricalEvents);
            Assert.Null(profile.FirstKnownPerformanceAt);
            Assert.Null(profile.LastKnownPerformanceAt);
            Assert.Equal(new[] { "Old Empty" }, profile.AliasNames);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static IDbContextFactory<MkddDbContext> CreateFactory(string path)
    {
        var options = new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new TestContextFactory(options);
    }

    private static MkddPlayer CreateModernPlayer(string name) => new()
    {
        SitePlayerId = 1,
        Name = name,
        Country = "FR"
    };

    private static MkddHistoricalPlayerAlias CreateAlias(
        string name,
        MkddPlayer? player,
        string sourceSystem) => new()
    {
        SourceSystem = sourceSystem,
        ExternalName = name,
        NormalizedExternalName = $"{sourceSystem}:{name.ToLowerInvariant()}:{Guid.NewGuid():N}",
        Player = player,
        Confidence = player is null ? 0 : 100,
        ResolutionStatus = player is null ? "Unresolved" : "Matched"
    };

    private static HistoricalPlayerOption Option(MkddHistoricalPlayerAlias alias) => new()
    {
        Key = $"alias:{alias.Id}",
        DisplayName = alias.ExternalName,
        HistoricalPlayerAliasId = alias.Id
    };

    private static MkddHistoricalPerformance CreatePerformance(
        string key,
        MkddHistoricalPlayerAlias alias,
        MkddCourse course,
        string type,
        int? frequency,
        long milliseconds,
        DateTimeOffset date,
        string source = "YahooGroups",
        string dateSource = "YahooFirstSeenDate") => new()
    {
        ExternalKey = key,
        HistoricalPlayerAlias = alias,
        Course = course,
        TimeType = type,
        Frequency = frequency,
        Time = TimeSpan.FromMilliseconds(milliseconds),
        FirstSeenAt = date,
        DateSource = dateSource,
        SequenceNumber = 1,
        OccurrenceCount = 1,
        SourceSystem = source,
        ImportStatus = "Approved"
    };

    private static DateTimeOffset Utc(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private sealed class TestContextFactory(DbContextOptions<MkddDbContext> options)
        : IDbContextFactory<MkddDbContext>
    {
        public MkddDbContext CreateDbContext() => new(options);

        public Task<MkddDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class RankingRequestCollector : IDisposable
    {
        private readonly List<HistoricalProfileRankingRequest> _requests = [];

        public IReadOnlyList<HistoricalProfileRankingRequest> Requests => _requests;

        public void Add(HistoricalProfileRankingRequest request) => _requests.Add(request);

        public void Dispose() => _requests.Clear();
    }
}
