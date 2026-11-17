using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Models.Dashboard;
using MKDDTracker.Scraper.Services.EvolutionService;

namespace MKDDTracker.Scraper.Services.Dashboard;

public sealed class MkddHistoricalDashboardService
{
    private readonly IDbContextFactory<MkddDbContext> _dbFactory;

    public MkddHistoricalDashboardService(
        IDbContextFactory<MkddDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<MkddHistoricalDashboard> GetDashboardAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<DashboardPerformance> performances = await db.HistoricalPerformances
            .AsNoTracking()
            .Select(performance => new DashboardPerformance(
                performance.Id,
                performance.HistoricalPlayerAliasId,
                performance.HistoricalPlayerAlias.PlayerId,
                performance.HistoricalPlayerAlias.Player != null
                    ? performance.HistoricalPlayerAlias.Player.Name
                    : performance.HistoricalPlayerAlias.ExternalName,
                performance.CourseId,
                performance.Course.Name,
                performance.TimeType,
                performance.Frequency,
                performance.Time,
                performance.FirstSeenAt,
                performance.SequenceNumber,
                performance.SourceSystem))
            .ToListAsync(cancellationToken);

        HashSet<HistoricalIdentity> identities = performances
            .Select(performance => performance.PlayerId is int playerId
                ? new HistoricalIdentity(playerId, null)
                : new HistoricalIdentity(null, performance.HistoricalPlayerAliasId))
            .ToHashSet();

        List<ConsolidatedHistoricalPerformance<DashboardPerformance>> consolidated =
            HistoricalPerformanceConsolidator.Consolidate(
                performances,
                performance => new HistoricalPerformanceDeduplicationKey(
                    performance.PlayerId is int playerId
                        ? $"player:{playerId}"
                        : $"alias:{performance.HistoricalPlayerAliasId}",
                    performance.CourseId,
                    performance.TimeType,
                    performance.Time,
                    DateOnly.FromDateTime(performance.FirstSeenAt.ToUniversalTime().UtcDateTime)),
                performance => performance.Frequency,
                performance => performance.SourceSystem,
                performance => performance.FirstSeenAt,
                performance => performance.SequenceNumber)
            .OrderByDescending(item => item.Preferred.FirstSeenAt.ToUniversalTime())
            .ThenByDescending(item => item.Preferred.SequenceNumber)
            .ThenByDescending(item => item.Preferred.Id)
            .Take(10)
            .ToList();

        return new MkddHistoricalDashboard(
            TotalPerformanceCount: performances.Count,
            HistoricalIdentityCount: identities.Count,
            ModernLinkedPlayerCount: performances
                .Where(performance => performance.PlayerId is not null)
                .Select(performance => performance.PlayerId)
                .Distinct()
                .Count(),
            CourseCount: performances
                .Select(performance => performance.CourseId)
                .Distinct()
                .Count(),
            FirstSeenAt: performances.Count == 0
                ? null
                : performances.Min(performance => performance.FirstSeenAt.ToUniversalTime()),
            LastSeenAt: performances.Count == 0
                ? null
                : performances.Max(performance => performance.FirstSeenAt.ToUniversalTime()),
            YahooGroupsCount: performances.Count(performance =>
                string.Equals(performance.SourceSystem, "YahooGroups", StringComparison.OrdinalIgnoreCase)),
            MkddTrackerCount: performances.Count(performance =>
                string.Equals(performance.SourceSystem, "MKDDTracker", StringComparison.OrdinalIgnoreCase)),
            ThreeLapCount: performances.Count(performance =>
                string.Equals(performance.TimeType, "ThreeLap", StringComparison.OrdinalIgnoreCase)),
            FlapCount: performances.Count(performance =>
                string.Equals(performance.TimeType, "Flap", StringComparison.OrdinalIgnoreCase)),
            Hz50Count: performances.Count(performance => performance.Frequency == 50),
            Hz60Count: performances.Count(performance => performance.Frequency == 60),
            UnknownFrequencyCount: performances.Count(performance =>
                performance.Frequency is not 50 and not 60),
            RecentEvents: consolidated
                .Select(item => new MkddHistoricalDashboardEvent(
                    item.Preferred.FirstSeenAt.ToUniversalTime(),
                    item.Preferred.PlayerName,
                    item.Preferred.PlayerId is int playerId
                        ? $"player:{playerId}"
                        : $"alias:{item.Preferred.HistoricalPlayerAliasId}",
                    item.Preferred.PlayerId,
                    item.Preferred.HistoricalPlayerAliasId,
                    item.Preferred.CourseName,
                    item.Preferred.TimeType,
                    item.Preferred.Frequency,
                    item.Preferred.Time,
                    item.Sources))
                .ToList());
    }

    private sealed record DashboardPerformance(
        long Id,
        int HistoricalPlayerAliasId,
        int? PlayerId,
        string PlayerName,
        int CourseId,
        string CourseName,
        string TimeType,
        int? Frequency,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        int SequenceNumber,
        string SourceSystem);

    private readonly record struct HistoricalIdentity(
        int? PlayerId,
        int? HistoricalPlayerAliasId);
}
