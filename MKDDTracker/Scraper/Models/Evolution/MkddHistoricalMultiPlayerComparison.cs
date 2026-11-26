namespace MKDDTracker.Scraper.Models.Evolution;

public sealed class MkddHistoricalMultiPlayerComparison
{
    public required string CourseName { get; init; }

    public required string TimeType { get; init; }

    public HistoricalFrequencyFilter FrequencyFilter { get; init; }

    public DateTimeOffset StartDate { get; init; }

    public DateTimeOffset EndDate { get; init; }

    public IReadOnlyList<MkddHistoricalPlayerComparisonEntry>
        Players
    { get; init; } = [];
}