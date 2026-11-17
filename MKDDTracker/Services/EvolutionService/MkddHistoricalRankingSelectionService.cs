using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddHistoricalRankingSelectionService
{
    private readonly IDbContextFactory<MkddDbContext> _dbFactory;

    public MkddHistoricalRankingSelectionService(
        IDbContextFactory<MkddDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<HistoricalPlayerOption>>
        GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var aliases = await db.HistoricalPlayerAliases
            .AsNoTracking()
            .Where(alias => alias.HistoricalPerformances.Any())
            .Select(alias => new
            {
                alias.Id,
                alias.PlayerId,
                DisplayName = alias.Player != null
                    ? alias.Player.Name
                    : alias.ExternalName
            })
            .ToListAsync(cancellationToken);

        return aliases
            .GroupBy(alias => alias.PlayerId is not null
                ? $"player:{alias.PlayerId.Value}"
                : $"alias:{alias.Id}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var alias = group
                    .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.Id)
                    .First();

                return new HistoricalPlayerOption
                {
                    Key = group.Key,
                    DisplayName = alias.DisplayName,
                    PlayerId = alias.PlayerId,
                    HistoricalPlayerAliasId = alias.Id
                };
            })
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<string>> GetCoursesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.HistoricalPerformances
            .AsNoTracking()
            .Select(performance => performance.Course.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
    }

    public async Task<HistoricalPerformanceDateRange> GetDateRangeAsync(
        HistoricalPlayerOption player,
        string courseName,
        string timeType,
        HistoricalFrequencyFilter frequencyFilter,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        IQueryable<MkddHistoricalPerformance> query = db.HistoricalPerformances
            .AsNoTracking()
            .Where(performance =>
                performance.Course.Name == courseName &&
                performance.TimeType == timeType);

        query = frequencyFilter switch
        {
            HistoricalFrequencyFilter.Hz50 => query.Where(performance => performance.Frequency == 50),
            HistoricalFrequencyFilter.Hz60 => query.Where(performance => performance.Frequency == 60),
            HistoricalFrequencyFilter.Unknown => query.Where(performance => performance.Frequency == null),
            _ => query.Where(_ => false)
        };

        query = player.PlayerId is not null
            ? query.Where(performance => performance.HistoricalPlayerAlias.PlayerId == player.PlayerId)
            : query.Where(performance => performance.HistoricalPlayerAliasId == player.HistoricalPlayerAliasId);

        List<DateTimeOffset> dates = await query
            .Select(performance => performance.FirstSeenAt)
            .ToListAsync(cancellationToken);

        return new HistoricalPerformanceDateRange
        {
            Count = dates.Count,
            FirstSeenAt = dates.Count == 0 ? null : dates.Min(),
            LastSeenAt = dates.Count == 0 ? null : dates.Max()
        };
    }
}
