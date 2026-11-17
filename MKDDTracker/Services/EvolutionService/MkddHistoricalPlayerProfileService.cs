using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddHistoricalPlayerProfileService
{
    private readonly IDbContextFactory<MkddDbContext> _dbFactory;
    private readonly Action<HistoricalProfileRankingRequest>? _rankingRequested;

    public MkddHistoricalPlayerProfileService(
        IDbContextFactory<MkddDbContext> dbFactory,
        Action<HistoricalProfileRankingRequest>? rankingRequested = null)
    {
        _dbFactory = dbFactory;
        _rankingRequested = rankingRequested;
    }

    public async Task<Dictionary<string, IReadOnlyList<string>>> GetAliasNamesAsync(
        IReadOnlyCollection<HistoricalPlayerOption> players,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var aliases = await db.HistoricalPlayerAliases
            .AsNoTracking()
            .Select(alias => new { alias.Id, alias.PlayerId, alias.ExternalName })
            .ToListAsync(cancellationToken);

        return players.ToDictionary(
            player => player.Key,
            player => (IReadOnlyList<string>)aliases
                .Where(alias => player.PlayerId is not null
                    ? alias.PlayerId == player.PlayerId
                    : alias.Id == player.HistoricalPlayerAliasId)
                .Select(alias => alias.ExternalName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<MkddHistoricalPlayerProfile?> GetProfileAsync(
        HistoricalPlayerOption player,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.PlayerId is null && player.HistoricalPlayerAliasId <= 0)
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var aliasesQuery = db.HistoricalPlayerAliases
            .AsNoTracking()
            .Where(alias => player.PlayerId != null
                ? alias.PlayerId == player.PlayerId
                : alias.Id == player.HistoricalPlayerAliasId);
        var aliases = await aliasesQuery
            .Select(alias => new
            {
                alias.Id,
                alias.ExternalName,
                ModernPlayerId = alias.PlayerId,
                ModernPlayerName = alias.Player != null ? alias.Player.Name : null,
                Country = alias.Player != null ? alias.Player.Country : null
            })
            .ToListAsync(cancellationToken);

        if (aliases.Count == 0)
            return null;

        var aliasIds = aliases.Select(alias => alias.Id).ToList();
        List<ProfilePerformance> performances = await db.HistoricalPerformances
            .AsNoTracking()
            .Where(performance => aliasIds.Contains(performance.HistoricalPlayerAliasId))
            .Select(performance => new ProfilePerformance(
                performance.HistoricalPlayerAliasId,
                performance.CourseId,
                performance.Course.Name,
                performance.TimeType,
                performance.Frequency,
                performance.Time,
                performance.FirstSeenAt,
                performance.SourceSystem))
            .ToListAsync(cancellationToken);

        List<string> aliasNames = aliases
            .Select(alias => alias.ExternalName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var modernAlias = aliases.FirstOrDefault(alias => alias.ModernPlayerId is not null);
        string displayName = player.PlayerId is not null
            ? modernAlias?.ModernPlayerName ?? player.DisplayName
            : aliases.FirstOrDefault()?.ExternalName ?? player.DisplayName;

        var rankingService = new MkddHistoricalRankingService(db);
        var timelineService = new MkddHistoricalPlayerTimelineService(db, rankingService);
        var rankingCache = new Dictionary<RankingRequestKey, MkddHistoricalRanking?>();
        var categories = new List<MkddHistoricalPlayerCategorySummary>();

        foreach (var series in performances
                     .Where(performance => IsSupportedTimeType(performance.TimeType))
                     .GroupBy(performance => new
                     {
                         performance.CourseId,
                         performance.CourseName,
                         TimeType = NormalizeTimeType(performance.TimeType)
                     })
                     .OrderBy(group => group.Key.CourseName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(group => group.Key.TimeType, StringComparer.OrdinalIgnoreCase))
        {
            DateTimeOffset firstSeenAt = series.Min(performance => performance.FirstSeenAt.ToUniversalTime());
            DateTimeOffset lastSeenAt = series.Max(performance => performance.FirstSeenAt.ToUniversalTime());
            DateOnly startDate = DateOnly.FromDateTime(firstSeenAt.UtcDateTime);
            DateOnly endDate = DateOnly.FromDateTime(lastSeenAt.UtcDateTime);
            DateTimeOffset startAt = new(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            DateTimeOffset endAt = new(endDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

            MkddHistoricalPlayerTimeline? timeline = await timelineService.GetTimelineForProfileAsync(
                player,
                series.Key.CourseName,
                series.Key.TimeType,
                HistoricalFrequencyFilter.All,
                startAt,
                endAt,
                cancellationToken);
            if (timeline is null)
                continue;

            foreach (var category in timeline.Points
                         .Select(point => new { Point = point, Filter = ToFrequencyFilter(point.Frequency) })
                         .GroupBy(item => item.Filter)
                         .OrderBy(group => (int)group.Key))
            {
                List<MkddHistoricalPlayerTimelinePoint> points = category
                    .Select(item => item.Point)
                    .OrderBy(point => point.Date.ToUniversalTime())
                    .ThenBy(point => point.SequenceNumber)
                    .ToList();
                if (points.Count == 0)
                    continue;

                MkddHistoricalPlayerTimelinePoint first = points[0];
                MkddHistoricalPlayerTimelinePoint last = points[^1];
                MkddHistoricalPlayerTimelinePoint best = points
                    .OrderBy(point => point.Time)
                    .ThenBy(point => point.Date.ToUniversalTime())
                    .ThenBy(point => point.SequenceNumber)
                    .First();
                DateOnly rankingDate = DateOnly.FromDateTime(last.Date.ToUniversalTime().UtcDateTime);
                var rankingKey = new RankingRequestKey(
                    series.Key.CourseId,
                    series.Key.TimeType,
                    category.Key,
                    rankingDate);

                if (!rankingCache.TryGetValue(rankingKey, out MkddHistoricalRanking? ranking))
                {
                    _rankingRequested?.Invoke(new HistoricalProfileRankingRequest(
                        series.Key.CourseId,
                        series.Key.CourseName,
                        series.Key.TimeType,
                        category.Key,
                        rankingDate));
                    DateTimeOffset rankingAt = new(
                        rankingDate.ToDateTime(TimeOnly.MaxValue),
                        TimeSpan.Zero);
                    ranking = await rankingService.GetRankingAtAsync(
                        series.Key.CourseName,
                        series.Key.TimeType,
                        category.Key,
                        rankingAt,
                        cancellationToken);
                    rankingCache.Add(rankingKey, ranking);
                }

                MkddHistoricalRankingEntry? rankingEntry = ranking?.Entries.FirstOrDefault(entry =>
                    player.PlayerId is not null
                        ? entry.PlayerId == player.PlayerId
                        : entry.HistoricalPlayerAliasId == player.HistoricalPlayerAliasId);

                categories.Add(new MkddHistoricalPlayerCategorySummary
                {
                    CourseId = series.Key.CourseId,
                    CourseName = series.Key.CourseName,
                    TimeType = series.Key.TimeType,
                    FrequencyFilter = category.Key,
                    FirstKnownTime = first.Time,
                    CurrentBestTime = best.Time,
                    FirstKnownAt = first.Date,
                    CurrentBestAt = best.Date,
                    LastKnownAt = last.Date,
                    Improvement = first.Time - best.Time,
                    HistoricalEventCount = points.Count,
                    CurrentRank = rankingEntry?.Rank,
                    RankedPlayerCount = ranking?.Entries.Count,
                    Sources = points
                        .SelectMany(point => point.Sources)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                });
            }
        }

        categories = categories
            .OrderBy(category => category.CourseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(category => category.TimeType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(category => (int)category.FrequencyFilter)
            .ToList();

        DateTimeOffset[] knownDates = categories
            .SelectMany(category => new[] { category.FirstKnownAt, category.LastKnownAt })
            .ToArray();

        return new MkddHistoricalPlayerProfile
        {
            PlayerKey = player.Key,
            DisplayName = displayName,
            PlayerId = player.PlayerId,
            Country = player.PlayerId is null ? null : modernAlias?.Country,
            IsLinkedToModernPlayer = player.PlayerId is not null,
            AliasNames = aliasNames,
            FirstKnownPerformanceAt = knownDates.Length == 0 ? null : knownDates.Min(),
            LastKnownPerformanceAt = knownDates.Length == 0 ? null : knownDates.Max(),
            TotalHistoricalEvents = categories.Sum(category => category.HistoricalEventCount),
            DistinctCoursesCount = categories.Select(category => category.CourseId).Distinct().Count(),
            Categories = categories
        };
    }

    private static bool IsSupportedTimeType(string timeType) =>
        string.Equals(timeType, "ThreeLap", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(timeType, "Flap", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeTimeType(string timeType) =>
        string.Equals(timeType, "Flap", StringComparison.OrdinalIgnoreCase)
            ? "Flap"
            : "ThreeLap";

    private static HistoricalFrequencyFilter ToFrequencyFilter(int? frequency) => frequency switch
    {
        50 => HistoricalFrequencyFilter.Hz50,
        60 => HistoricalFrequencyFilter.Hz60,
        _ => HistoricalFrequencyFilter.Unknown
    };

    private sealed record ProfilePerformance(
        int HistoricalPlayerAliasId,
        int CourseId,
        string CourseName,
        string TimeType,
        int? Frequency,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        string SourceSystem);

    private readonly record struct RankingRequestKey(
        int CourseId,
        string TimeType,
        HistoricalFrequencyFilter Frequency,
        DateOnly Date);
}

public sealed record HistoricalProfileRankingRequest(
    int CourseId,
    string CourseName,
    string TimeType,
    HistoricalFrequencyFilter Frequency,
    DateOnly Date);
