using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;
using MKDDTracker.Scraper.Services.Ranking;

namespace MKDDTracker.Scraper.Services;

/// <summary>
/// Creates an explicit, synthetic demonstration history.
/// Call this service only with the dedicated demo database.
/// </summary>
public sealed class MkddDemoDataSeeder
{
    private static readonly DateOnly[] DemoDates =
    [
        new(2026, 9, 1),
        new(2026, 9, 8),
        new(2026, 9, 15),
        new(2026, 9, 22)
    ];

    private readonly MkddDbContext _db;

    public MkddDemoDataSeeder(MkddDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(
        string courseName,
        string mattildeName,
        string enzoName,
        IReadOnlyList<MkddPerformance> sourcePerformances)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(courseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(mattildeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(enzoName);
        ArgumentNullException.ThrowIfNull(sourcePerformances);

        if (sourcePerformances.Count == 0)
        {
            throw new ArgumentException(
                "At least one source performance is required.",
                nameof(sourcePerformances));
        }

        var course = await GetOrCreateCourseAsync(courseName);
        var playersBySiteId = await GetOrCreatePlayersAsync(sourcePerformances);

        var mattilde = FindPlayer(playersBySiteId, mattildeName);
        var enzo = FindPlayer(playersBySiteId, enzoName);
        var timeOverrides = CreateTimeOverrides(mattilde.Id, enzo.Id);

        foreach (var date in DemoDates)
        {
            var exists = await _db.Snapshots.AnyAsync(x =>
                x.CourseId == course.Id &&
                x.RankingDate == date);

            if (exists)
            {
                continue;
            }

            var snapshot = new MkddSnapshot
            {
                CourseId = course.Id,
                RankingDate = date,
                CapturedAt = date.ToDateTime(new TimeOnly(12, 0))
            };

            var temporaryPerformances = sourcePerformances
                .Where(x => playersBySiteId.ContainsKey(x.SitePlayerId))
                .Select(x =>
                {
                    var player = playersBySiteId[x.SitePlayerId];

                    return new DemoPerformance(
                        player.Id,
                        GetBestTimeSoFar(
                            player.Id,
                            date,
                            timeOverrides,
                            x.Time),
                        x.Standard,
                        x.RecordDate,
                        x.VideoUrl);
                })
                .ToList();

            var rankedPerformances = MkddRankingCalculator.CalculateRanks(
                temporaryPerformances,
                x => x.Time);

            foreach (var rankedPerformance in rankedPerformances)
            {
                var performance = rankedPerformance.Item;

                snapshot.Performances.Add(
                    new MkddPerformanceEntity
                    {
                        PlayerId = performance.PlayerId,
                        Rank = rankedPerformance.Rank,
                        Time = performance.Time,
                        Standard = performance.Standard,
                        RecordDate = performance.RecordDate,
                        VideoUrl = performance.VideoUrl
                    });
            }

            _db.Snapshots.Add(snapshot);
        }

        await _db.SaveChangesAsync();
    }

    private async Task<MkddCourse> GetOrCreateCourseAsync(string courseName)
    {
        var course = await _db.Courses
            .SingleOrDefaultAsync(x => x.Name == courseName);

        if (course is not null)
        {
            return course;
        }

        course = new MkddCourse
        {
            Name = courseName
        };

        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        return course;
    }

    private async Task<Dictionary<int, MkddPlayer>> GetOrCreatePlayersAsync(
        IReadOnlyList<MkddPerformance> sourcePerformances)
    {
        var sourceBySitePlayerId = sourcePerformances
            .GroupBy(x => x.SitePlayerId)
            .ToDictionary(x => x.Key, x => x.First());

        var sitePlayerIds = sourceBySitePlayerId.Keys.ToList();
        var existingPlayers = await _db.Players
            .Where(x => sitePlayerIds.Contains(x.SitePlayerId))
            .ToListAsync();

        var playersBySiteId = existingPlayers
            .ToDictionary(x => x.SitePlayerId);

        foreach (var source in sourceBySitePlayerId.Values)
        {
            if (playersBySiteId.ContainsKey(source.SitePlayerId))
            {
                continue;
            }

            var player = new MkddPlayer
            {
                SitePlayerId = source.SitePlayerId,
                Name = source.PlayerName,
                Country = source.Country
            };

            _db.Players.Add(player);
            playersBySiteId.Add(player.SitePlayerId, player);
        }

        await _db.SaveChangesAsync();

        return playersBySiteId;
    }

    private static MkddPlayer FindPlayer(
        IReadOnlyDictionary<int, MkddPlayer> playersBySiteId,
        string playerName) =>
        playersBySiteId.Values.SingleOrDefault(x =>
            string.Equals(
                x.Name,
                playerName,
                StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Demo player '{playerName}' is absent from the source ranking.");

    private static Dictionary<(int PlayerId, DateOnly Date), TimeSpan>
        CreateTimeOverrides(int mattildeId, int enzoId) =>
        new()
        {
            [(mattildeId, new DateOnly(2026, 9, 1))] =
                TimeSpan.FromMilliseconds(74_642),
            [(mattildeId, new DateOnly(2026, 9, 8))] =
                TimeSpan.FromMilliseconds(74_510),
            [(mattildeId, new DateOnly(2026, 9, 15))] =
                TimeSpan.FromMilliseconds(74_401),
            [(mattildeId, new DateOnly(2026, 9, 22))] =
                TimeSpan.FromMilliseconds(74_210),
            [(enzoId, new DateOnly(2026, 9, 1))] =
                TimeSpan.FromMilliseconds(74_500),
            [(enzoId, new DateOnly(2026, 9, 8))] =
                TimeSpan.FromMilliseconds(74_500),
            [(enzoId, new DateOnly(2026, 9, 15))] =
                TimeSpan.FromMilliseconds(74_200),
            [(enzoId, new DateOnly(2026, 9, 22))] =
                TimeSpan.FromMilliseconds(74_200)
        };

    private static TimeSpan GetBestTimeSoFar(
        int playerId,
        DateOnly date,
        IReadOnlyDictionary<(int PlayerId, DateOnly Date), TimeSpan> overrides,
        TimeSpan originalTime)
    {
        var bestTime = originalTime;

        foreach (var entry in overrides)
        {
            if (entry.Key.PlayerId == playerId &&
                entry.Key.Date <= date &&
                entry.Value < bestTime)
            {
                bestTime = entry.Value;
            }
        }

        return bestTime;
    }

    private sealed record DemoPerformance(
        int PlayerId,
        TimeSpan Time,
        string Standard,
        DateOnly RecordDate,
        string? VideoUrl);
}
