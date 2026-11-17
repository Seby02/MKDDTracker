namespace MKDDTracker.Scraper.Models.Evolution;

public sealed class MkddHistoricalPlayerTimeline
{
    public required string PlayerName { get; init; }

    public required string CourseName { get; init; }

    public required string TimeType { get; init; }

    public HistoricalFrequencyFilter FrequencyFilter { get; init; }

    public DateTimeOffset StartDate { get; init; }

    public DateTimeOffset EndDate { get; init; }

    public IReadOnlyList<MkddHistoricalPlayerTimelinePoint> Points { get; init; } = [];
}

public sealed class MkddHistoricalPlayerTimelinePoint
{
    public DateTimeOffset Date { get; init; }

    public TimeSpan Time { get; init; }

    public int? Frequency { get; init; }

    public int Rank { get; init; }

    public TimeSpan? PreviousTime { get; init; }

    public int? PreviousRank { get; init; }

    public TimeSpan? TimeImprovement { get; init; }

    public int? PlacesGained { get; init; }

    public int HistoricalPlayerAliasId { get; init; }

    public int? PlayerId { get; init; }

    public required string SourceSystem { get; init; }

    public required string DateSource { get; init; }

    public int SequenceNumber { get; init; }

    public int OccurrenceCount { get; init; }

    public IReadOnlyList<string> Sources { get; init; } = [];
}
