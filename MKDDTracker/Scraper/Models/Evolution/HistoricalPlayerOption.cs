namespace MKDDTracker.Scraper.Models.Evolution;

public sealed class HistoricalPlayerOption
{
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public int? PlayerId { get; init; }

    public int HistoricalPlayerAliasId { get; init; }
}

public sealed class HistoricalPerformanceDateRange
{
    public int Count { get; init; }

    public DateTimeOffset? FirstSeenAt { get; init; }

    public DateTimeOffset? LastSeenAt { get; init; }
}
