using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.Ranking;
using MKDDTracker.Scraper.Services.SnapshotService;
using MovementAnalysis = MKDDTracker.Scraper.Models.Evolution.MkddRankMovementAnalysis;
using Xunit;

namespace MKDDTracker.Tests;

public sealed class MkddPlayerMovementTests
{
    private static readonly DateOnly PreviousDate = new(2026, 9, 1);
    private static readonly DateOnly CurrentDate = new(2026, 9, 8);

    [Fact]
    public async Task TargetImprovement_PassesStationaryPlayer()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Alice"] = 80_000,
                ["Bob"] = 81_000
            },
            new Dictionary<string, double>
            {
                ["Bob"] = 79_500,
                ["Alice"] = 80_000
            });

        var analysis = await CompareAsync(db, "Bob");

        Assert.NotNull(analysis);
        Assert.Equal(1, analysis.TotalPlacesGained);
        Assert.True(analysis.HasPersonalImprovement);

        var movement = Assert.Single(analysis.PlayersWhoMoved);
        Assert.Equal("Alice", movement.PlayerName);
        Assert.Equal(MkddMovementRelation.TargetPassedPlayer, movement.Relation);
        Assert.Equal(MkddMovementCause.TargetDriven, movement.Cause);
        Assert.Equal(-1, movement.RankDelta);
        Assert.Equal(-1, movement.RankChange);
        Assert.Equal(analysis.DirectMovements, analysis.PlayersWhoMoved);
        Assert.NotEqual(0, movement.RelatedPlayerId);

        var report = new MkddEvolutionReportService().BuildReport(
            new MkddCompletePlayerEvolution(
                analysis.PlayerName,
                analysis.CourseName,
                new[]
                {
                    new MkddPlayerEvolutionStep(
                        analysis.PreviousDate,
                        analysis.CurrentDate,
                        analysis.PreviousRank,
                        analysis.CurrentRank,
                        analysis.PreviousTime,
                        analysis.CurrentTime,
                        analysis.CounterfactualRank,
                        analysis.TotalPlacesGained,
                        analysis.PlacesGainedFromOwnImprovement,
                        analysis.PlacesGainedFromOthers,
                        analysis.PlayersWhoMoved,
                        analysis.RankImpacts)
                }));

        Assert.Single(report.DirectMovements);
    }

    [Fact]
    public async Task OpponentImprovement_PassesStationaryTargetWithoutTargetRegression()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Alice"] = 80_000,
                ["Bob"] = 81_000
            },
            new Dictionary<string, double>
            {
                ["Bob"] = 79_000,
                ["Alice"] = 80_000
            });

        var analysis = await CompareAsync(db, "Alice");

        Assert.NotNull(analysis);
        Assert.Equal(-1, analysis.TotalPlacesGained);
        Assert.False(analysis.HasPersonalImprovement);
        Assert.Equal(TimeSpan.Zero, analysis.TimeDelta);

        var movement = Assert.Single(analysis.PlayersWhoMoved);
        Assert.Equal("Bob", movement.PlayerName);
        Assert.Equal(MkddMovementRelation.PlayerPassedTarget, movement.Relation);
        Assert.Equal(MkddMovementCause.OpponentDriven, movement.Cause);
    }

    [Fact]
    public async Task PersonalImprovementWithoutRankChange_HasNoDirectMovement()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Bob"] = 79_000,
                ["Alice"] = 80_500
            },
            new Dictionary<string, double>
            {
                ["Bob"] = 79_000,
                ["Alice"] = 80_250
            });

        var analysis = await CompareAsync(db, "Alice");

        Assert.NotNull(analysis);
        Assert.True(analysis.HasPersonalImprovement);
        Assert.Equal(0, analysis.RankDelta);
        Assert.Equal(TimeSpan.FromMilliseconds(-250), analysis.TimeDelta);
        Assert.Empty(analysis.PlayersWhoMoved);
    }

    [Fact]
    public async Task EqualTime_CreatesTieWithoutArbitraryPlayerIdTieBreak()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Alice"] = 80_000,
                ["Bob"] = 81_000
            },
            new Dictionary<string, double>
            {
                ["Alice"] = 80_000,
                ["Bob"] = 80_000
            });

        var analysis = await CompareAsync(db, "Bob");

        Assert.NotNull(analysis);
        Assert.Equal(1, analysis.CurrentRank);

        var movement = Assert.Single(analysis.PlayersWhoMoved);
        Assert.Equal(MkddMovementRelation.TieCreated, movement.Relation);
        Assert.Equal(MkddMovementCause.TargetDriven, movement.Cause);
        Assert.Equal(1, movement.NewRank);
    }

    [Fact]
    public async Task TieBroken_IsReportedExplicitly()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Alice"] = 80_000,
                ["Bob"] = 80_000
            },
            new Dictionary<string, double>
            {
                ["Bob"] = 79_500,
                ["Alice"] = 80_000
            });

        var analysis = await CompareAsync(db, "Bob");

        Assert.NotNull(analysis);
        var movement = Assert.Single(analysis.PlayersWhoMoved);
        Assert.Equal(MkddMovementRelation.TieBroken, movement.Relation);
        Assert.Equal(MkddMovementCause.TargetDriven, movement.Cause);
    }

    [Fact]
    public async Task NewPlayer_IsRepresentedWithMissingPreviousState()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Alice"] = 80_000
            },
            new Dictionary<string, double>
            {
                ["Bob"] = 79_000,
                ["Alice"] = 80_000
            });

        var analysis = await CompareAsync(db, "Alice");

        Assert.NotNull(analysis);
        Assert.Equal(-1, analysis.TotalPlacesGained);

        var movement = Assert.Single(analysis.PlayersWhoMoved);
        Assert.Equal("Bob", movement.PlayerName);
        Assert.Equal(MkddMovementRelation.NewEntry, movement.Relation);
        Assert.Equal(MkddMovementCause.NewEntry, movement.Cause);
        Assert.Null(movement.OldRank);
        Assert.Null(movement.OldTime);
        Assert.Equal(1, movement.NewRank);
        Assert.Null(movement.RankDelta);
        Assert.False(movement.WasPresentBefore);
        Assert.True(movement.IsPresentNow);
    }

    [Fact]
    public async Task RemovedPlayer_IsRepresentedWithMissingCurrentState()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Bob"] = 79_000,
                ["Alice"] = 80_000
            },
            new Dictionary<string, double>
            {
                ["Alice"] = 80_000
            });

        var analysis = await CompareAsync(db, "Alice");

        Assert.NotNull(analysis);
        Assert.Equal(1, analysis.TotalPlacesGained);

        var movement = Assert.Single(analysis.PlayersWhoMoved);
        Assert.Equal("Bob", movement.PlayerName);
        Assert.Equal(MkddMovementRelation.PlayerRemoved, movement.Relation);
        Assert.Equal(MkddMovementCause.PlayerRemoved, movement.Cause);
        Assert.Equal(1, movement.OldRank);
        Assert.Null(movement.NewRank);
        Assert.Null(movement.NewTime);
        Assert.Null(movement.RankDelta);
        Assert.True(movement.WasPresentBefore);
        Assert.False(movement.IsPresentNow);
    }

    [Fact]
    public async Task ThreePlaceGain_ReturnsEveryCrossedPlayer()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Alice"] = 78_000,
                ["Claire"] = 79_000,
                ["David"] = 80_000,
                ["Bob"] = 81_000
            },
            new Dictionary<string, double>
            {
                ["Bob"] = 77_500,
                ["Alice"] = 78_000,
                ["Claire"] = 79_000,
                ["David"] = 80_000
            });

        var analysis = await CompareAsync(db, "Bob");

        Assert.NotNull(analysis);
        Assert.Equal(3, analysis.TotalPlacesGained);
        Assert.Equal(4, analysis.PreviousRank);
        Assert.Equal(1, analysis.CurrentRank);
        Assert.Equal(3, analysis.PlayersWhoMoved.Count);
        Assert.All(
            analysis.PlayersWhoMoved,
            movement =>
            {
                Assert.Equal(MkddMovementRelation.TargetPassedPlayer, movement.Relation);
                Assert.Equal(MkddMovementCause.TargetDriven, movement.Cause);
            });
        Assert.Equal(
            new[] { "Alice", "Claire", "David" },
            analysis.PlayersWhoMoved
                .Select(x => x.PlayerName)
                .OrderBy(x => x)
                .ToArray());
    }

    [Fact]
    public async Task RankChangesWithoutRelativeCrossing_AreNotReportedAsDirectMovements()
    {
        await using var db = await CreateComparisonAsync(
            new Dictionary<string, double>
            {
                ["Alice"] = 78_000,
                ["Bob"] = 80_000,
                ["Claire"] = 81_000
            },
            new Dictionary<string, double>
            {
                ["New Leader"] = 77_000,
                ["Alice"] = 78_000,
                ["Bob"] = 80_000,
                ["Claire"] = 81_000
            });

        var analysis = await CompareAsync(db, "Bob");

        Assert.NotNull(analysis);
        var movement = Assert.Single(analysis.PlayersWhoMoved);
        Assert.Equal("New Leader", movement.PlayerName);
        Assert.Equal(MkddMovementRelation.NewEntry, movement.Relation);
        Assert.DoesNotContain(
            analysis.PlayersWhoMoved,
            x => x.PlayerName is "Alice" or "Claire");
    }

    private static async Task<MovementAnalysis?> CompareAsync(
        MkddDbContext db,
        string targetPlayer) =>
        await new MkddSnapshotComparisonService(db).CompareAsync(
            targetPlayer,
            "Luigi Circuit",
            PreviousDate,
            CurrentDate);

    private static async Task<MkddDbContext> CreateComparisonAsync(
        IReadOnlyDictionary<string, double> previousTimes,
        IReadOnlyDictionary<string, double> currentTimes)
    {
        var options = new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        var db = new MkddDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();

        var playerNames = previousTimes.Keys
            .Union(currentTimes.Keys)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var players = playerNames
            .Select((name, index) => new MkddPlayer
            {
                SitePlayerId = index + 1,
                Name = name,
                Country = "France"
            })
            .ToDictionary(x => x.Name, StringComparer.Ordinal);

        var course = new MkddCourse { Name = "Luigi Circuit" };
        db.Courses.Add(course);
        db.Players.AddRange(players.Values);
        await db.SaveChangesAsync();

        db.Snapshots.Add(CreateSnapshot(
            course,
            players,
            PreviousDate,
            previousTimes));

        db.Snapshots.Add(CreateSnapshot(
            course,
            players,
            CurrentDate,
            currentTimes));

        await db.SaveChangesAsync();
        return db;
    }

    private static MkddSnapshot CreateSnapshot(
        MkddCourse course,
        IReadOnlyDictionary<string, MkddPlayer> players,
        DateOnly date,
        IReadOnlyDictionary<string, double> times)
    {
        var timedPlayers = times
            .Select(x => new TimedPlayer(
                players[x.Key],
                TimeSpan.FromMilliseconds(x.Value)))
            .ToList();

        var snapshot = new MkddSnapshot
        {
            Course = course,
            RankingDate = date,
            CapturedAt = date.ToDateTime(
                new TimeOnly(12, 0),
                DateTimeKind.Utc)
        };

        foreach (var ranked in MkddRankingCalculator.CalculateRanks(
                     timedPlayers,
                     x => x.Time))
        {
            snapshot.Performances.Add(
                new MkddPerformanceEntity
                {
                    Player = ranked.Item.Player,
                    Rank = ranked.Rank,
                    Time = ranked.Item.Time,
                    RecordDate = date
                });
        }

        return snapshot;
    }

    private sealed record TimedPlayer(
        MkddPlayer Player,
        TimeSpan Time);
}
