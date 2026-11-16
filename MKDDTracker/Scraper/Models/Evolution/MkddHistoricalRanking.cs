namespace MKDDTracker.Scraper.Models.Evolution;

public sealed class MkddHistoricalRanking
{
    public required string CourseName { get; init; }

    public required string TimeType { get; init; }

    public HistoricalFrequencyFilter FrequencyFilter { get; init; }

    public DateTimeOffset RankingAt { get; init; }

    public List<MkddHistoricalRankingEntry> Entries { get; init; } = [];
}

public sealed class MkddHistoricalRankingEntry
{
    public int Rank { get; init; }

    public int HistoricalPlayerAliasId { get; init; }

    public int? PlayerId { get; init; }

    public required string PlayerName { get; init; }

    public required string ExternalName { get; init; }

    public string? Country { get; init; }

    public required string CourseName { get; init; }

    public required string TimeType { get; init; }

    public int? Frequency { get; init; }

    public TimeSpan Time { get; init; }

    public DateTimeOffset FirstSeenAt { get; init; }

    public int SequenceNumber { get; init; }

    public int OccurrenceCount { get; init; }

    public bool IsLinkedToModernPlayer =>
        PlayerId != null;
}