using MKDDTracker.Scraper.Models.Evolution;

using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddHistoricalRankingComparisonService
{
    private readonly MkddHistoricalRankingService _rankingService;

    public MkddHistoricalRankingComparisonService(
        MkddHistoricalRankingService rankingService)
    {
        _rankingService = rankingService;
    }

    public async Task<MkddHistoricalRankingComparison>
        CompareAsync(
            string courseName,
            string timeType,
            HistoricalFrequencyFilter frequencyFilter,
            DateTimeOffset initialDate,
            DateTimeOffset finalDate,
            CancellationToken cancellationToken = default)
    {
        MkddHistoricalRanking? initialRanking =
            await _rankingService.GetRankingAtAsync(
                courseName,
                timeType,
                frequencyFilter,
                initialDate,
                cancellationToken);

        MkddHistoricalRanking? finalRanking =
            await _rankingService.GetRankingAtAsync(
                courseName,
                timeType,
                frequencyFilter,
                finalDate,
                cancellationToken);

        if (initialRanking is null || finalRanking is null)
        {
            return new MkddHistoricalRankingComparison
            {
                CourseName = courseName,
                TimeType = timeType,
                FrequencyFilter = frequencyFilter,
                InitialDate = initialDate.ToUniversalTime(),
                FinalDate = finalDate.ToUniversalTime(),
                Status = MkddHistoricalRankingComparisonStatus.Unavailable
            };
        }

        var initialByIdentity = initialRanking.Entries
            .ToDictionary(GetIdentityKey, StringComparer.OrdinalIgnoreCase);
        var finalByIdentity = finalRanking.Entries
            .ToDictionary(GetIdentityKey, StringComparer.OrdinalIgnoreCase);

        var identities = initialByIdentity.Keys
            .Union(finalByIdentity.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = identities
            .Select(identity => CreateEntry(
                initialByIdentity.GetValueOrDefault(identity),
                finalByIdentity.GetValueOrDefault(identity)))
            .OrderByDescending(entry => entry.FinalRank.HasValue)
            .ThenBy(entry => entry.FinalRank ?? int.MaxValue)
            .ThenBy(entry => entry.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new MkddHistoricalRankingComparison
        {
            CourseName = courseName,
            TimeType = timeType,
            FrequencyFilter = frequencyFilter,
            InitialDate = initialRanking.RankingAt,
            FinalDate = finalRanking.RankingAt,
            Status = MkddHistoricalRankingComparisonStatus.Compared,
            Entries = entries
        };
    }

    private static MkddHistoricalRankingComparisonEntry CreateEntry(
        MkddHistoricalRankingEntry? initial,
        MkddHistoricalRankingEntry? final)
    {
        MkddHistoricalRankingEntry source = final ?? initial!;
        bool isCompared = initial is not null && final is not null;

        return new MkddHistoricalRankingComparisonEntry
        {
            PlayerName = source.PlayerName,
            ExternalName = source.ExternalName,
            PlayerId = source.PlayerId,
            HistoricalPlayerAliasId = source.HistoricalPlayerAliasId,
            InitialRank = initial?.Rank,
            FinalRank = final?.Rank,
            InitialTime = initial?.Time,
            FinalTime = final?.Time,
            PlacesGained = isCompared
                ? initial!.Rank - final!.Rank
                : null,
            TimeImprovement = isCompared
                ? initial!.Time - final!.Time
                : null,
            Status = isCompared
                ? MkddHistoricalRankingComparisonStatus.Compared
                : final is not null
                    ? MkddHistoricalRankingComparisonStatus.NewEntrant
                    : MkddHistoricalRankingComparisonStatus.MissingAtEnd
        };
    }

    private static string GetIdentityKey(
        MkddHistoricalRankingEntry entry)
    {
        return entry.PlayerId is not null
            ? $"player:{entry.PlayerId.Value}"
            : $"alias:{entry.HistoricalPlayerAliasId}";
    }
}
