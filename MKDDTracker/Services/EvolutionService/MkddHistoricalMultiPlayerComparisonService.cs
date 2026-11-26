using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddHistoricalMultiPlayerComparisonService
{
    private readonly MkddHistoricalRankingService _rankingService;
    private readonly MkddHistoricalPlayerTimelineService _timelineService;

    public MkddHistoricalMultiPlayerComparisonService(
        MkddHistoricalRankingService rankingService,
        MkddHistoricalPlayerTimelineService timelineService)
    {
        _rankingService = rankingService;
        _timelineService = timelineService;
    }

    public async Task<MkddHistoricalMultiPlayerComparison?> CompareAsync(
        IReadOnlyCollection<int> playerIds,
        string courseName,
        string timeType,
        HistoricalFrequencyFilter frequencyFilter,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        if (playerIds.Count == 0)
        {
            throw new ArgumentException(
                "Au moins un joueur doit être sélectionné.",
                nameof(playerIds));
        }

        if (string.IsNullOrWhiteSpace(courseName))
        {
            throw new ArgumentException(
                "Le circuit est obligatoire.",
                nameof(courseName));
        }

        string normalizedTimeType =
            NormalizeTimeType(timeType);

        DateTimeOffset normalizedStartDate =
            startDate.ToUniversalTime();

        DateTimeOffset normalizedEndDate =
            endDate.ToUniversalTime();

        if (normalizedStartDate > normalizedEndDate)
        {
            throw new ArgumentException(
                "La date de début doit être antérieure ou égale à la date de fin.",
                nameof(startDate));
        }

        int[] distinctPlayerIds =
            playerIds
                .Distinct()
                .ToArray();

        if (distinctPlayerIds.Any(playerId => playerId <= 0))
        {
            throw new ArgumentException(
                "Les identifiants des joueurs doivent être strictement positifs.",
                nameof(playerIds));
        }

        MkddHistoricalRanking? initialRanking =
            await _rankingService.GetRankingAtAsync(
                courseName.Trim(),
                normalizedTimeType,
                frequencyFilter,
                normalizedStartDate,
                cancellationToken);

        MkddHistoricalRanking? finalRanking =
            await _rankingService.GetRankingAtAsync(
                courseName.Trim(),
                normalizedTimeType,
                frequencyFilter,
                normalizedEndDate,
                cancellationToken);

        //
        // Aucun classement aux deux bornes signifie qu'aucune donnée
        // n'existe pour cette catégorie sur cette période.
        //

        if (initialRanking is null &&
            finalRanking is null)
        {
            return null;
        }

        Dictionary<int, MkddHistoricalRankingEntry> initialByPlayerId =
            BuildPlayerLookup(initialRanking);

        Dictionary<int, MkddHistoricalRankingEntry> finalByPlayerId =
            BuildPlayerLookup(finalRanking);

        var entries =
            new List<MkddHistoricalPlayerComparisonEntry>(
                distinctPlayerIds.Length);

        foreach (int playerId in distinctPlayerIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            initialByPlayerId.TryGetValue(
                playerId,
                out MkddHistoricalRankingEntry? initial);

            finalByPlayerId.TryGetValue(
                playerId,
                out MkddHistoricalRankingEntry? final);

            //
            // Pour cette première étape, un joueur qui n'apparaît
            // à aucune borne n'est pas retourné.
            //
            // Nous ajouterons ensuite une résolution des joueurs
            // indépendante du classement.
            //

            if (initial is null &&
                final is null)
            {
                continue;
            }
            MkddHistoricalRankingEntry referenceEntry = final ?? initial!;

            var playerOption =
                new HistoricalPlayerOption
                {
                    Key =
                        $"player:{playerId}",

                    DisplayName =
                        referenceEntry.PlayerName,

                    PlayerId =
                        playerId,

                    HistoricalPlayerAliasId =
                        referenceEntry.HistoricalPlayerAliasId
                };

            MkddHistoricalPlayerTimeline? timeline =
                await _timelineService.GetTimelineAsync(
                    playerOption,
                    courseName.Trim(),
                    normalizedTimeType,
                    frequencyFilter,
                    normalizedStartDate,
                    normalizedEndDate,
                    cancellationToken);

            int prCount =
                timeline?.Points.Count
                ?? 0;

            TimeSpan? initialTime =
                initial?.Time;

            TimeSpan? finalTime =
                final?.Time;

            int? initialRank =
                initial?.Rank;

            int? finalRank =
                final?.Rank;

            TimeSpan? timeImprovement =
                initialTime.HasValue &&
                finalTime.HasValue
                    ? initialTime.Value - finalTime.Value
                    : null;

            int? placesGained =
                initialRank.HasValue &&
                finalRank.HasValue
                    ? initialRank.Value - finalRank.Value
                    : null;

            entries.Add(
                new MkddHistoricalPlayerComparisonEntry
                {
                    PlayerId =
                        playerId,

                    PlayerName =
                        final?.PlayerName
                        ?? initial!.PlayerName,

                    InitialTime =
                        initialTime,

                    FinalTime =
                        finalTime,

                    TimeImprovement =
                        timeImprovement,

                    InitialRank =
                        initialRank,

                    FinalRank =
                        finalRank,

                    PlacesGained =
                        placesGained,

                    //
                    // Étape suivante :
                    // calcul à partir de MkddHistoricalPlayerTimelineService.
                    //
                    PrCount =
                        prCount
                });
        }

        MkddHistoricalRanking referenceRanking =
            finalRanking
            ?? initialRanking!;

        return new MkddHistoricalMultiPlayerComparison
        {
            CourseName =
                referenceRanking.CourseName,

            TimeType =
                referenceRanking.TimeType,

            FrequencyFilter =
                frequencyFilter,

            StartDate =
                normalizedStartDate,

            EndDate =
                normalizedEndDate,

            Players =
                entries
        };
    }

    private static Dictionary<int, MkddHistoricalRankingEntry>
        BuildPlayerLookup(
            MkddHistoricalRanking? ranking)
    {
        if (ranking is null)
        {
            return [];
        }

        return ranking.Entries
            .Where(entry =>
                entry.PlayerId.HasValue)
            .GroupBy(entry =>
                entry.PlayerId!.Value)
            .ToDictionary(
                group =>
                    group.Key,
                group =>
                    group.First());
    }

    private static string NormalizeTimeType(
        string timeType)
    {
        if (string.IsNullOrWhiteSpace(timeType))
        {
            throw new ArgumentException(
                "Le type de chrono est obligatoire.",
                nameof(timeType));
        }

        string normalized =
            timeType.Trim();

        if (string.Equals(
                normalized,
                "ThreeLap",
                StringComparison.OrdinalIgnoreCase))
        {
            return "ThreeLap";
        }

        if (string.Equals(
                normalized,
                "Flap",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Flap";
        }

        throw new ArgumentOutOfRangeException(
            nameof(timeType),
            timeType,
            "Le type de chrono doit être ThreeLap ou Flap.");
    }
}