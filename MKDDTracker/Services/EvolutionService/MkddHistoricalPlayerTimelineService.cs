using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddHistoricalPlayerTimelineService
{
    private readonly MkddDbContext _db;
    private readonly MkddHistoricalRankingService _rankingService;
    private readonly Action<DateOnly>? _rankingDateRequested;

    public MkddHistoricalPlayerTimelineService(
        MkddDbContext db,
        MkddHistoricalRankingService rankingService,
        Action<DateOnly>? rankingDateRequested = null)
    {
        _db = db;
        _rankingService = rankingService;
        _rankingDateRequested = rankingDateRequested;
    }

    public async Task<MkddHistoricalPlayerTimeline?> GetTimelineAsync(
        HistoricalPlayerOption player,
        string courseName,
        string timeType,
        HistoricalFrequencyFilter frequencyFilter,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (string.IsNullOrWhiteSpace(courseName) ||
            !IsSupportedTimeType(timeType) ||
            startDate.ToUniversalTime() > endDate.ToUniversalTime() ||
            (player.PlayerId is null && player.HistoricalPlayerAliasId <= 0))
        {
            return null;
        }

        IQueryable<MkddHistoricalPerformance> query = _db.HistoricalPerformances
            .AsNoTracking()
            .Where(performance =>
                performance.Course.Name == courseName &&
                performance.TimeType == timeType);

        query = player.PlayerId is not null
            ? query.Where(performance =>
                performance.HistoricalPlayerAlias.PlayerId == player.PlayerId)
            : query.Where(performance =>
                performance.HistoricalPlayerAliasId == player.HistoricalPlayerAliasId);

        List<TimelinePerformance> loadedPerformances = await query
            .Select(performance => new TimelinePerformance(
                performance.HistoricalPlayerAliasId,
                performance.HistoricalPlayerAlias.PlayerId,
                performance.CourseId,
                performance.TimeType,
                performance.Frequency,
                performance.Time,
                performance.FirstSeenAt,
                performance.SequenceNumber,
                performance.OccurrenceCount,
                performance.SourceSystem,
                performance.DateSource,
                performance.ExternalKey))
            .ToListAsync(cancellationToken);

        DateTimeOffset normalizedStartDate = startDate.ToUniversalTime();
        DateTimeOffset normalizedEndDate = endDate.ToUniversalTime();

        List<TimelinePerformance> inPeriod = loadedPerformances
            .Where(performance =>
            {
                DateTimeOffset firstSeenAt = performance.FirstSeenAt.ToUniversalTime();
                return firstSeenAt >= normalizedStartDate && firstSeenAt <= normalizedEndDate;
            })
            .ToList();

        if (inPeriod.Count == 0)
            return null;

        List<ConsolidatedPerformance> consolidatedPerformances = inPeriod
            .GroupBy(performance => new TimelineDeduplicationKey(
                BuildIdentityKey(performance, player),
                performance.CourseId,
                performance.TimeType,
                performance.Time,
                DateOnly.FromDateTime(performance.FirstSeenAt.UtcDateTime)))
            .Select(group => Consolidate(group))
            .Where(item => MatchesFrequencyFilter(item.Preferred.Frequency, frequencyFilter))
            .OrderBy(item => item.Preferred.FirstSeenAt.ToUniversalTime())
            .ThenBy(item => item.Preferred.SequenceNumber)
            .ToList();

        var rankingByDate = new Dictionary<DateOnly, MkddHistoricalRanking?>();
        var points = new List<MkddHistoricalPlayerTimelinePoint>(consolidatedPerformances.Count);
        TimeSpan? previousTime = null;
        int? previousRank = null;

        foreach (ConsolidatedPerformance consolidated in consolidatedPerformances)
        {
            TimelinePerformance performance = consolidated.Preferred;
            DateTimeOffset utcFirstSeenAt = performance.FirstSeenAt.ToUniversalTime();
            DateOnly utcDate = DateOnly.FromDateTime(utcFirstSeenAt.UtcDateTime);

            if (!rankingByDate.TryGetValue(utcDate, out MkddHistoricalRanking? ranking))
            {
                DateTimeOffset rankingAtEndOfDay = new(
                    utcDate.ToDateTime(TimeOnly.MaxValue),
                    TimeSpan.Zero);
                _rankingDateRequested?.Invoke(utcDate);
                ranking = await _rankingService.GetRankingAtAsync(
                    courseName,
                    timeType,
                    frequencyFilter,
                    rankingAtEndOfDay,
                    cancellationToken);
                rankingByDate.Add(utcDate, ranking);
            }

            MkddHistoricalRankingEntry? rankingEntry = ranking?.Entries
                .FirstOrDefault(entry => player.PlayerId is not null
                    ? entry.PlayerId == player.PlayerId
                    : entry.HistoricalPlayerAliasId == player.HistoricalPlayerAliasId);

            if (rankingEntry is null)
                continue;

            points.Add(new MkddHistoricalPlayerTimelinePoint
            {
                Date = utcFirstSeenAt,
                Time = performance.Time,
                Frequency = performance.Frequency,
                Rank = rankingEntry.Rank,
                PreviousTime = previousTime,
                PreviousRank = previousRank,
                TimeImprovement = previousTime is null
                    ? null
                    : previousTime.Value - performance.Time,
                PlacesGained = previousRank is null
                    ? null
                    : previousRank.Value - rankingEntry.Rank,
                HistoricalPlayerAliasId = performance.HistoricalPlayerAliasId,
                PlayerId = performance.PlayerId,
                SourceSystem = performance.SourceSystem,
                DateSource = performance.DateSource,
                SequenceNumber = performance.SequenceNumber,
                OccurrenceCount = performance.OccurrenceCount,
                Sources = consolidated.Sources
            });

            previousTime = performance.Time;
            previousRank = rankingEntry.Rank;
        }

        return points.Count == 0
            ? null
            : new MkddHistoricalPlayerTimeline
            {
                PlayerName = player.DisplayName,
                CourseName = courseName,
                TimeType = timeType,
                FrequencyFilter = frequencyFilter,
                StartDate = normalizedStartDate,
                EndDate = normalizedEndDate,
                Points = points
            };
    }

    private static bool MatchesFrequencyFilter(
        int? frequency,
        HistoricalFrequencyFilter frequencyFilter)
    {
        return frequencyFilter switch
        {
            HistoricalFrequencyFilter.All => true,
            HistoricalFrequencyFilter.Hz50 => frequency == 50,
            HistoricalFrequencyFilter.Hz60 => frequency == 60,
            HistoricalFrequencyFilter.Unknown => frequency is null,
            _ => false
        };
    }

    private static ConsolidatedPerformance Consolidate(
        IEnumerable<TimelinePerformance> performances)
    {
        List<TimelinePerformance> group = performances.ToList();
        TimelinePerformance preferred = group
            .OrderByDescending(performance => performance.Frequency.HasValue)
            .ThenByDescending(performance =>
                performance.Frequency.HasValue &&
                string.Equals(performance.SourceSystem, "YahooGroups", StringComparison.OrdinalIgnoreCase))
            .ThenBy(performance => performance.FirstSeenAt.ToUniversalTime())
            .ThenBy(performance => performance.SequenceNumber)
            .First();

        IReadOnlyList<string> sources = group
            .Select(performance => performance.SourceSystem)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ConsolidatedPerformance(preferred, sources);
    }

    private static string BuildIdentityKey(
        TimelinePerformance performance,
        HistoricalPlayerOption player)
    {
        return player.PlayerId is not null
            ? $"player:{player.PlayerId.Value}"
            : $"alias:{performance.HistoricalPlayerAliasId}";
    }

    private static bool IsSupportedTimeType(string timeType) =>
        string.Equals(timeType, "ThreeLap", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(timeType, "Flap", StringComparison.OrdinalIgnoreCase);

    private sealed record TimelinePerformance(
        int HistoricalPlayerAliasId,
        int? PlayerId,
        int CourseId,
        string TimeType,
        int? Frequency,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        int SequenceNumber,
        int OccurrenceCount,
        string SourceSystem,
        string DateSource,
        string ExternalKey);

    private sealed record ConsolidatedPerformance(
        TimelinePerformance Preferred,
        IReadOnlyList<string> Sources);

    private readonly record struct TimelineDeduplicationKey(
        string PlayerIdentity,
        int CourseId,
        string TimeType,
        TimeSpan Time,
        DateOnly UtcDate);
}
