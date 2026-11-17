
using MKDDTracker.Scraper.Models.Evolution;
namespace MKDDTracker.Scraper.Models.Evolution;

public enum MkddHistoricalRankingComparisonStatus
{
    Compared,
    NewEntrant,
    MissingAtEnd,
    Unavailable
}

public sealed class MkddHistoricalRankingComparison
{
    public required string CourseName { get; init; }

    public required string TimeType { get; init; }

    public HistoricalFrequencyFilter FrequencyFilter { get; init; }

    public DateTimeOffset InitialDate { get; init; }

    public DateTimeOffset FinalDate { get; init; }

    public MkddHistoricalRankingComparisonStatus Status { get; init; }

    public List<MkddHistoricalRankingComparisonEntry> Entries { get; init; } = [];
}

public sealed class MkddHistoricalRankingComparisonEntry
{
    public required string PlayerName { get; init; }

    public required string ExternalName { get; init; }

    public int? PlayerId { get; init; }

    public int? HistoricalPlayerAliasId { get; init; }

    public int? InitialRank { get; init; }

    public int? FinalRank { get; init; }

    public TimeSpan? InitialTime { get; init; }

    public TimeSpan? FinalTime { get; init; }

    public int? PlacesGained { get; init; }

    public TimeSpan? TimeImprovement { get; init; }

    public MkddHistoricalRankingComparisonStatus Status { get; init; }
}
