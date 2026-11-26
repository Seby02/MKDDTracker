namespace MKDDTracker.Scraper.Models.Evolution;

public sealed class MkddHistoricalPlayerComparisonEntry
{
    public int PlayerId { get; init; }

    public required string PlayerName { get; init; }

    public TimeSpan? InitialTime { get; init; }

    public TimeSpan? FinalTime { get; init; }

    public TimeSpan? TimeImprovement { get; init; }

    public int? InitialRank { get; init; }

    public int? FinalRank { get; init; }

    public int? PlacesGained { get; init; }

    public int PrCount { get; init; }

    public bool IsNewEntry =>
        InitialTime is null &&
        FinalTime is not null;

    public bool HasData =>
        InitialTime is not null ||
        FinalTime is not null;
}