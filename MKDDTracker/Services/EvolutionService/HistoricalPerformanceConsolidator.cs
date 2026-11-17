namespace MKDDTracker.Scraper.Services.EvolutionService;

internal static class HistoricalPerformanceConsolidator
{
    public static IReadOnlyList<ConsolidatedHistoricalPerformance<T>> Consolidate<T>(
        IEnumerable<T> performances,
        Func<T, HistoricalPerformanceDeduplicationKey> keySelector,
        Func<T, int?> frequencySelector,
        Func<T, string> sourceSelector,
        Func<T, DateTimeOffset> firstSeenAtSelector,
        Func<T, int> sequenceNumberSelector,
        bool preserveFrequencyCategories = false)
    {
        return performances
            .GroupBy(keySelector)
            .SelectMany(group => ConsolidateGroup(
                group,
                frequencySelector,
                sourceSelector,
                firstSeenAtSelector,
                sequenceNumberSelector,
                preserveFrequencyCategories))
            .ToList();
    }

    private static IEnumerable<ConsolidatedHistoricalPerformance<T>> ConsolidateGroup<T>(
        IEnumerable<T> performances,
        Func<T, int?> frequencySelector,
        Func<T, string> sourceSelector,
        Func<T, DateTimeOffset> firstSeenAtSelector,
        Func<T, int> sequenceNumberSelector,
        bool preserveFrequencyCategories)
    {
        List<T> group = performances.ToList();
        int knownFrequencyCount = group
            .Where(performance => frequencySelector(performance).HasValue)
            .Select(frequencySelector)
            .Distinct()
            .Count();

        if (!preserveFrequencyCategories || knownFrequencyCount <= 1)
            return [CreateConsolidated(
                group,
                frequencySelector,
                sourceSelector,
                firstSeenAtSelector,
                sequenceNumberSelector)];

        return group
            .GroupBy(frequencySelector)
            .Select(frequencyGroup => CreateConsolidated(
                frequencyGroup,
                frequencySelector,
                sourceSelector,
                firstSeenAtSelector,
                sequenceNumberSelector));
    }

    private static ConsolidatedHistoricalPerformance<T> CreateConsolidated<T>(
        IEnumerable<T> candidates,
        Func<T, int?> frequencySelector,
        Func<T, string> sourceSelector,
        Func<T, DateTimeOffset> firstSeenAtSelector,
        Func<T, int> sequenceNumberSelector)
    {
        List<T> candidatesList = candidates.ToList();
        T preferred = candidatesList
            .OrderByDescending(candidate => frequencySelector(candidate).HasValue)
            .ThenByDescending(candidate =>
                frequencySelector(candidate).HasValue &&
                string.Equals(
                    sourceSelector(candidate),
                    "YahooGroups",
                    StringComparison.OrdinalIgnoreCase))
            .ThenBy(candidate => firstSeenAtSelector(candidate).ToUniversalTime())
            .ThenBy(sequenceNumberSelector)
            .First();

        IReadOnlyList<string> sources = candidatesList
            .Select(sourceSelector)
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ConsolidatedHistoricalPerformance<T>(preferred, sources);
    }
}

internal readonly record struct HistoricalPerformanceDeduplicationKey(
    string PlayerIdentity,
    int CourseId,
    string TimeType,
    TimeSpan Time,
    DateOnly UtcDate);

internal sealed record ConsolidatedHistoricalPerformance<T>(
    T Preferred,
    IReadOnlyList<string> Sources);
