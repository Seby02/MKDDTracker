using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddHistoricalRankingService
{
    private readonly IDbContextFactory<MkddDbContext>? _dbFactory;
    private readonly MkddDbContext? _db;

    public MkddHistoricalRankingService(
        IDbContextFactory<MkddDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public MkddHistoricalRankingService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<MkddHistoricalRanking?>
        GetRankingAtAsync(
            string courseName,
            string timeType,
            HistoricalFrequencyFilter frequencyFilter,
            DateTimeOffset rankingAt,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(courseName))
            return null;

        if (!IsSupportedTimeType(timeType))
            return null;

        MkddDbContext db = _db ?? await _dbFactory!.CreateDbContextAsync(cancellationToken);

        //
        // SQLite présente des limitations pour comparer et
        // ordonner directement des DateTimeOffset.
        //
        // On filtre d'abord en SQL sur les critères fortement
        // sélectifs : circuit, type et fréquence.
        //

        IQueryable<MkddHistoricalPerformance> performanceQuery =
            db.HistoricalPerformances
                .AsNoTracking()
                .Where(performance =>
                    performance.Course.Name == courseName
                    &&
                    performance.TimeType == timeType);

        performanceQuery =
            ApplyFrequencyFilter(
                performanceQuery,
                frequencyFilter);

        IQueryable<HistoricalRankingCandidate> query =
            performanceQuery
                .Select(performance =>
                    new HistoricalRankingCandidate(
                        performance.HistoricalPlayerAliasId,

                        performance.HistoricalPlayerAlias.PlayerId,

                        performance.HistoricalPlayerAlias.Player != null
                            ? performance.HistoricalPlayerAlias.Player.Name
                            : null,

                        performance.HistoricalPlayerAlias.Player != null
                            ? performance.HistoricalPlayerAlias.Player.Country
                            : null,

                        performance.HistoricalPlayerAlias.ExternalName,

                        performance.Course.Name,

                        performance.TimeType,

                        performance.Frequency,

                        performance.Time,

                        performance.FirstSeenAt,

                        performance.SequenceNumber,

                        performance.OccurrenceCount));

        List<HistoricalRankingCandidate> candidates =
            await query.ToListAsync(
                cancellationToken);

        DateTimeOffset normalizedRankingAt =
            rankingAt.ToUniversalTime();

        //
        // Ne conserver que les performances connues avant
        // la date demandée.
        //

        List<HistoricalRankingCandidate> candidatesAtDate =
            candidates
                .Where(candidate =>
                    candidate.FirstSeenAt.ToUniversalTime()
                    <= normalizedRankingAt)
                .ToList();

        if (candidatesAtDate.Count == 0)
            return null;

        //
        // Plusieurs alias Yahoo peuvent être liés au même
        // joueur MKDDTracker.
        //
        // Si PlayerId existe :
        //     toutes les variantes de nom sont regroupées.
        //
        // Sinon :
        //     l'alias Yahoo constitue l'identité historique.
        //

        List<HistoricalRankingCandidate> latestByPlayer =
            candidatesAtDate
                .GroupBy(
                    BuildPlayerIdentityKey,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    group
                        .OrderByDescending(candidate =>
                            candidate.FirstSeenAt)
                        .ThenByDescending(candidate =>
                            candidate.SequenceNumber)
                        .ThenBy(candidate =>
                            candidate.Time)
                        .First())
                .OrderBy(candidate =>
                    candidate.Time)
                .ThenBy(candidate =>
                    candidate.DisplayName,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        List<MkddHistoricalRankingEntry> entries =
            CalculateRanks(
                latestByPlayer);

        return new MkddHistoricalRanking
        {
            CourseName =
                courseName,

            TimeType =
                timeType,

            FrequencyFilter =
                frequencyFilter,

            RankingAt =
                normalizedRankingAt,

            Entries =
                entries
        };
    }

    public async Task<MkddHistoricalRankingEntry?>
        GetPlayerRankingAtAsync(
            string playerName,
            string courseName,
            string timeType,
            HistoricalFrequencyFilter frequencyFilter,
            DateTimeOffset rankingAt,
            CancellationToken cancellationToken = default)
    {
        MkddHistoricalRanking? ranking =
            await GetRankingAtAsync(
                courseName,
                timeType,
                frequencyFilter,
                rankingAt,
                cancellationToken);

        if (ranking is null)
            return null;

        return ranking.Entries
            .FirstOrDefault(entry =>
                string.Equals(
                    entry.PlayerName,
                    playerName,
                    StringComparison.OrdinalIgnoreCase)
                ||
                string.Equals(
                    entry.ExternalName,
                    playerName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static IQueryable<MkddHistoricalPerformance>
    ApplyFrequencyFilter(
        IQueryable<MkddHistoricalPerformance> query,
        HistoricalFrequencyFilter frequencyFilter)
    {
        return frequencyFilter switch
        {
            HistoricalFrequencyFilter.Hz50 =>
                query.Where(performance =>
                    performance.Frequency == 50),

            HistoricalFrequencyFilter.Hz60 =>
                query.Where(performance =>
                    performance.Frequency == 60),

            HistoricalFrequencyFilter.Unknown =>
                query.Where(performance =>
                    performance.Frequency == null),

            _ =>
                query
        };
    }

    private static List<MkddHistoricalRankingEntry>
        CalculateRanks(
            IReadOnlyList<HistoricalRankingCandidate> candidates)
    {
        var entries =
            new List<MkddHistoricalRankingEntry>(
                candidates.Count);

        int rank = 0;
        int position = 0;

        TimeSpan? previousTime = null;

        foreach (HistoricalRankingCandidate candidate
                 in candidates)
        {
            position++;

            //
            // Classement de compétition :
            //
            // 1, 2, 2, 4
            //
            // Deux chronos strictement identiques ont le
            // même rang.
            //

            if (previousTime == null ||
                candidate.Time != previousTime.Value)
            {
                rank = position;
            }

            entries.Add(
                new MkddHistoricalRankingEntry
                {
                    Rank =
                        rank,

                    HistoricalPlayerAliasId =
                        candidate.HistoricalPlayerAliasId,

                    PlayerId =
                        candidate.PlayerId,

                    PlayerName =
                        candidate.DisplayName,

                    ExternalName =
                        candidate.ExternalName,

                    Country =
                        candidate.Country,

                    CourseName =
                        candidate.CourseName,

                    TimeType =
                        candidate.TimeType,

                    Frequency =
                        candidate.Frequency,

                    Time =
                        candidate.Time,

                    FirstSeenAt =
                        candidate.FirstSeenAt,

                    SequenceNumber =
                        candidate.SequenceNumber,

                    OccurrenceCount =
                        candidate.OccurrenceCount
                });

            previousTime =
                candidate.Time;
        }

        return entries;
    }

    private static string BuildPlayerIdentityKey(
        HistoricalRankingCandidate candidate)
    {
        if (candidate.PlayerId != null)
        {
            return
                $"player:{candidate.PlayerId.Value}";
        }

        return
            $"alias:{candidate.HistoricalPlayerAliasId}";
    }

    private static bool IsSupportedTimeType(
        string? timeType)
    {
        return string.Equals(
                   timeType,
                   "ThreeLap",
                   StringComparison.OrdinalIgnoreCase)
               ||
               string.Equals(
                   timeType,
                   "Flap",
                   StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HistoricalRankingCandidate(
        int HistoricalPlayerAliasId,
        int? PlayerId,
        string? ModernPlayerName,
        string? Country,
        string ExternalName,
        string CourseName,
        string TimeType,
        int? Frequency,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        int SequenceNumber,
        int OccurrenceCount)
    {
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(ModernPlayerName)
                ? ModernPlayerName
                : ExternalName;
    }
}