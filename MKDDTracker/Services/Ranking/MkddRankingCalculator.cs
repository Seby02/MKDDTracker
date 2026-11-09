namespace MKDDTracker.Scraper.Services.Ranking;

/// <summary>
/// Applies MKDD competition ranking: equal times receive the same rank and the
/// following rank skips the occupied positions (1, 2, 2, 4).
/// </summary>
public static class MkddRankingCalculator
{
    public static IReadOnlyList<MkddRankedItem<T>> CalculateRanks<T>(
        IEnumerable<T> performances,
        Func<T, TimeSpan> timeSelector)
    {
        ArgumentNullException.ThrowIfNull(performances);
        ArgumentNullException.ThrowIfNull(timeSelector);

        var orderedPerformances = performances
            .Select((item, index) => new
            {
                Item = item,
                OriginalIndex = index,
                Time = timeSelector(item)
            })
            .OrderBy(x => x.Time)
            .ThenBy(x => x.OriginalIndex)
            .ToList();

        var result = new List<MkddRankedItem<T>>(orderedPerformances.Count);
        TimeSpan? previousTime = null;
        var currentRank = 0;

        for (var index = 0; index < orderedPerformances.Count; index++)
        {
            var performance = orderedPerformances[index];

            if (previousTime is null || performance.Time != previousTime.Value)
            {
                currentRank = index + 1;
                previousTime = performance.Time;
            }

            result.Add(new MkddRankedItem<T>(performance.Item, currentRank));
        }

        return result;
    }

    public static int CalculateRank(
        IEnumerable<TimeSpan> competitorTimes,
        TimeSpan targetTime)
    {
        ArgumentNullException.ThrowIfNull(competitorTimes);

        return competitorTimes.Count(time => time < targetTime) + 1;
    }
}

public sealed record MkddRankedItem<T>(T Item, int Rank);
