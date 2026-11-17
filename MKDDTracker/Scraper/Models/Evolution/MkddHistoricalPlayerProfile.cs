namespace MKDDTracker.Scraper.Models.Evolution;

public sealed class MkddHistoricalPlayerProfile
{
    public required string PlayerKey { get; init; }

    public required string DisplayName { get; init; }

    public int? PlayerId { get; init; }

    public string? Country { get; init; }

    public bool IsLinkedToModernPlayer { get; init; }

    public IReadOnlyList<string> AliasNames { get; init; } = [];

    public DateTimeOffset? FirstKnownPerformanceAt { get; init; }

    public DateTimeOffset? LastKnownPerformanceAt { get; init; }

    public int TotalHistoricalEvents { get; init; }

    public int DistinctCoursesCount { get; init; }

    public IReadOnlyList<MkddHistoricalPlayerCategorySummary> Categories { get; init; } = [];
}

public sealed class MkddHistoricalPlayerCategorySummary
{
    public int CourseId { get; init; }

    public required string CourseName { get; init; }

    public required string TimeType { get; init; }

    public HistoricalFrequencyFilter FrequencyFilter { get; init; }

    public TimeSpan FirstKnownTime { get; init; }

    public TimeSpan CurrentBestTime { get; init; }

    public DateTimeOffset FirstKnownAt { get; init; }

    public DateTimeOffset CurrentBestAt { get; init; }

    public DateTimeOffset LastKnownAt { get; init; }

    public TimeSpan Improvement { get; init; }

    public int HistoricalEventCount { get; init; }

    public int? CurrentRank { get; init; }

    public int? RankedPlayerCount { get; init; }

    public IReadOnlyList<string> Sources { get; init; } = [];
}
