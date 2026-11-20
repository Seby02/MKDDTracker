using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;

namespace MKDDTracker.Scraper.Services;

/// <summary>
/// Creates an explicit and deterministic demonstration history.
///
/// The demonstration database uses HistoricalPerformances as its
/// source of truth and does not create legacy snapshots.
/// </summary>
public sealed class MkddDemoDataSeeder
{
    private const string SourceSystem =
        "MKDDTrackerDemo";

    private const string DateSource =
        "DemoSeed";

    private const string TimeType =
        "ThreeLap";

    private static readonly DateOnly[] DemoDates =
    [
        new(2026, 9, 1),
        new(2026, 9, 8),
        new(2026, 9, 15),
        new(2026, 9, 22)
    ];

    private readonly MkddDbContext _db;

    public MkddDemoDataSeeder(
        MkddDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(
        string courseName,
        string mattildeName,
        string enzoName,
        IReadOnlyList<MkddPerformance> sourcePerformances,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            courseName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            mattildeName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            enzoName);

        ArgumentNullException.ThrowIfNull(
            sourcePerformances);

        if (sourcePerformances.Count == 0)
        {
            throw new ArgumentException(
                "At least one source performance is required.",
                nameof(sourcePerformances));
        }

        MkddCourse course =
            await GetOrCreateCourseAsync(
                courseName,
                cancellationToken);

        Dictionary<int, MkddPlayer> playersBySiteId =
            await GetOrCreatePlayersAsync(
                sourcePerformances,
                cancellationToken);

        MkddPlayer mattilde =
            FindPlayer(
                playersBySiteId,
                mattildeName);

        MkddPlayer enzo =
            FindPlayer(
                playersBySiteId,
                enzoName);

        Dictionary<int, MkddHistoricalPlayerAlias>
            aliasesByPlayerId =
                await GetOrCreateAliasesAsync(
                    playersBySiteId.Values,
                    cancellationToken);

        Dictionary<(int PlayerId, DateOnly Date), TimeSpan>
            timeOverrides =
                CreateTimeOverrides(
                    mattilde.Id,
                    enzo.Id);

        List<DemoHistoricalEvent> eventPlans =
            BuildEventPlans(
                course,
                sourcePerformances,
                playersBySiteId,
                timeOverrides);

        HashSet<string> existingKeys =
            (await _db.HistoricalPerformances
                .AsNoTracking()
                .Where(performance =>
                    performance.SourceSystem ==
                        SourceSystem)
                .Select(performance =>
                    performance.ExternalKey)
                .ToListAsync(
                    cancellationToken))
                .ToHashSet(
                    StringComparer.Ordinal);

        foreach (DemoHistoricalEvent eventPlan
                 in eventPlans)
        {
            if (existingKeys.Contains(
                    eventPlan.ExternalKey))
            {
                continue;
            }

            MkddHistoricalPlayerAlias alias =
                aliasesByPlayerId[
                    eventPlan.PlayerId];

            _db.HistoricalPerformances.Add(
                new MkddHistoricalPerformance
                {
                    ExternalKey =
                        eventPlan.ExternalKey,

                    HistoricalPlayerAliasId =
                        alias.Id,

                    CourseId =
                        course.Id,

                    TimeType =
                        TimeType,

                    Frequency =
                        null,

                    Time =
                        eventPlan.Time,

                    FirstSeenAt =
                        eventPlan.FirstSeenAt,

                    DateSource =
                        DateSource,

                    SequenceNumber =
                        eventPlan.SequenceNumber,

                    PreviousTime =
                        eventPlan.PreviousTime,

                    Improvement =
                        eventPlan.Improvement,

                    OccurrenceCount =
                        1,

                    SourceSystem =
                        SourceSystem,

                    ImportStatus =
                        "Approved"
                });
        }

        await _db.SaveChangesAsync(
            cancellationToken);
    }

    private List<DemoHistoricalEvent> BuildEventPlans(
        MkddCourse course,
        IReadOnlyList<MkddPerformance> sourcePerformances,
        IReadOnlyDictionary<int, MkddPlayer>
            playersBySiteId,
        IReadOnlyDictionary<
            (int PlayerId, DateOnly Date),
            TimeSpan> timeOverrides)
    {
        var events =
            new List<DemoHistoricalEvent>();

        foreach (MkddPerformance source
                 in sourcePerformances)
        {
            if (!playersBySiteId.TryGetValue(
                    source.SitePlayerId,
                    out MkddPlayer? player))
            {
                continue;
            }

            TimeSpan? previousBest = null;
            int sequenceNumber = 0;

            foreach (DateOnly date in DemoDates)
            {
                TimeSpan candidateTime =
                    GetBestTimeSoFar(
                        player.Id,
                        date,
                        timeOverrides,
                        source.Time);

                //
                // The demo history is event-based:
                //
                // - first known time;
                // - then strict improvements only.
                //

                if (previousBest is not null &&
                    candidateTime >=
                        previousBest.Value)
                {
                    continue;
                }

                sequenceNumber++;

                DateTimeOffset firstSeenAt =
                    new(
                        date.ToDateTime(
                            new TimeOnly(12, 0)),
                        TimeSpan.Zero);

                TimeSpan? improvement =
                    previousBest is null
                        ? null
                        : previousBest.Value -
                          candidateTime;

                string externalKey =
                    BuildExternalKey(
                        player.Id,
                        course.Id,
                        candidateTime,
                        firstSeenAt);

                events.Add(
                    new DemoHistoricalEvent(
                        player.Id,
                        candidateTime,
                        firstSeenAt,
                        sequenceNumber,
                        previousBest,
                        improvement,
                        externalKey));

                previousBest =
                    candidateTime;
            }
        }

        return events;
    }

    private async Task<MkddCourse>
        GetOrCreateCourseAsync(
            string courseName,
            CancellationToken cancellationToken)
    {
        MkddCourse? course =
            await _db.Courses
                .SingleOrDefaultAsync(
                    course =>
                        course.Name == courseName,
                    cancellationToken);

        if (course is not null)
        {
            return course;
        }

        course =
            new MkddCourse
            {
                Name =
                    courseName
            };

        _db.Courses.Add(
            course);

        await _db.SaveChangesAsync(
            cancellationToken);

        return course;
    }

    private async Task<Dictionary<int, MkddPlayer>>
        GetOrCreatePlayersAsync(
            IReadOnlyList<MkddPerformance>
                sourcePerformances,
            CancellationToken cancellationToken)
    {
        Dictionary<int, MkddPerformance>
            sourceBySitePlayerId =
                sourcePerformances
                    .GroupBy(performance =>
                        performance.SitePlayerId)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.First());

        List<int> sitePlayerIds =
            sourceBySitePlayerId.Keys
                .ToList();

        List<MkddPlayer> existingPlayers =
            await _db.Players
                .Where(player =>
                    sitePlayerIds.Contains(
                        player.SitePlayerId))
                .ToListAsync(
                    cancellationToken);

        Dictionary<int, MkddPlayer>
            playersBySiteId =
                existingPlayers
                    .ToDictionary(player =>
                        player.SitePlayerId);

        foreach (MkddPerformance source
                 in sourceBySitePlayerId.Values)
        {
            if (playersBySiteId.ContainsKey(
                    source.SitePlayerId))
            {
                continue;
            }

            var player =
                new MkddPlayer
                {
                    SitePlayerId =
                        source.SitePlayerId,

                    Name =
                        source.PlayerName,

                    Country =
                        source.Country
                };

            _db.Players.Add(
                player);

            playersBySiteId.Add(
                player.SitePlayerId,
                player);
        }

        await _db.SaveChangesAsync(
            cancellationToken);

        return playersBySiteId;
    }

    private async Task<
        Dictionary<int, MkddHistoricalPlayerAlias>>
        GetOrCreateAliasesAsync(
            IEnumerable<MkddPlayer> players,
            CancellationToken cancellationToken)
    {
        List<MkddPlayer> playerList =
            players.ToList();

        List<int> playerIds =
            playerList
                .Select(player =>
                    player.Id)
                .ToList();

        List<MkddHistoricalPlayerAlias>
            existingAliases =
                await _db.HistoricalPlayerAliases
                    .Where(alias =>
                        alias.SourceSystem ==
                            SourceSystem
                        &&
                        alias.PlayerId != null
                        &&
                        playerIds.Contains(
                            alias.PlayerId.Value))
                    .ToListAsync(
                        cancellationToken);

        Dictionary<int, MkddHistoricalPlayerAlias>
            aliasesByPlayerId =
                existingAliases
                    .Where(alias =>
                        alias.PlayerId != null)
                    .ToDictionary(
                        alias =>
                            alias.PlayerId!.Value);

        foreach (MkddPlayer player
                 in playerList)
        {
            if (aliasesByPlayerId.ContainsKey(
                    player.Id))
            {
                continue;
            }

            var alias =
                new MkddHistoricalPlayerAlias
                {
                    SourceSystem =
                        SourceSystem,

                    ExternalName =
                        player.Name,

                    NormalizedExternalName =
                        NormalizeName(
                            player.Name),

                    PlayerId =
                        player.Id,

                    Country =
                        player.Country,

                    Confidence =
                        100,

                    ResolutionStatus =
                        "Matched"
                };

            _db.HistoricalPlayerAliases.Add(
                alias);

            aliasesByPlayerId.Add(
                player.Id,
                alias);
        }

        await _db.SaveChangesAsync(
            cancellationToken);

        return aliasesByPlayerId;
    }

    private static MkddPlayer FindPlayer(
        IReadOnlyDictionary<int, MkddPlayer>
            playersBySiteId,
        string playerName) =>
        playersBySiteId.Values
            .SingleOrDefault(player =>
                string.Equals(
                    player.Name,
                    playerName,
                    StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Demo player '{playerName}' is absent " +
            "from the source ranking.");

    private static Dictionary<
        (int PlayerId, DateOnly Date),
        TimeSpan>
        CreateTimeOverrides(
            int mattildeId,
            int enzoId) =>
        new()
        {
            [
                (
                    mattildeId,
                    new DateOnly(2026, 9, 1)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_642),

            [
                (
                    mattildeId,
                    new DateOnly(2026, 9, 8)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_510),

            [
                (
                    mattildeId,
                    new DateOnly(2026, 9, 15)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_401),

            [
                (
                    mattildeId,
                    new DateOnly(2026, 9, 22)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_210),

            [
                (
                    enzoId,
                    new DateOnly(2026, 9, 1)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_500),

            [
                (
                    enzoId,
                    new DateOnly(2026, 9, 8)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_500),

            [
                (
                    enzoId,
                    new DateOnly(2026, 9, 15)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_200),

            [
                (
                    enzoId,
                    new DateOnly(2026, 9, 22)
                )
            ] =
                TimeSpan.FromMilliseconds(
                    74_200)
        };

    private static TimeSpan GetBestTimeSoFar(
        int playerId,
        DateOnly date,
        IReadOnlyDictionary<
            (int PlayerId, DateOnly Date),
            TimeSpan> overrides,
        TimeSpan originalTime)
    {
        TimeSpan bestTime =
            originalTime;

        foreach (KeyValuePair<
                     (int PlayerId, DateOnly Date),
                     TimeSpan> entry
                 in overrides)
        {
            if (entry.Key.PlayerId ==
                    playerId
                &&
                entry.Key.Date <=
                    date
                &&
                entry.Value <
                    bestTime)
            {
                bestTime =
                    entry.Value;
            }
        }

        return bestTime;
    }

    private static string BuildExternalKey(
        int playerId,
        int courseId,
        TimeSpan time,
        DateTimeOffset firstSeenAt)
    {
        string rawKey =
            string.Join(
                "|",
                SourceSystem,
                playerId.ToString(
                    CultureInfo.InvariantCulture),
                courseId.ToString(
                    CultureInfo.InvariantCulture),
                TimeType,
                "UNKNOWN",
                ((long)time.TotalMilliseconds)
                    .ToString(
                        CultureInfo.InvariantCulture),
                firstSeenAt.UtcDateTime.Ticks
                    .ToString(
                        CultureInfo.InvariantCulture),
                DateSource);

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    rawKey)));
    }

    private static string NormalizeName(
        string value)
    {
        string normalized =
            value
                .Trim()
                .Normalize(
                    NormalizationForm.FormKC);

        return Regex.Replace(
                normalized,
                @"\s+",
                " ")
            .ToLowerInvariant();
    }

    private sealed record DemoHistoricalEvent(
        int PlayerId,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        int SequenceNumber,
        TimeSpan? PreviousTime,
        TimeSpan? Improvement,
        string ExternalKey);
}