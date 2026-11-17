namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotHistoryMigrationResult
{
    public DateOnly? EarliestRecordDate { get; init; }

    public DateOnly? LatestRecordDate { get; init; }

    public DateOnly? EarliestRankingDate { get; init; }

    public DateOnly? LatestRankingDate { get; init; }

    public int SnapshotPerformancesAnalyzed { get; init; }

    public int PlayerCourseSeriesAnalyzed { get; init; }

    public int ExistingModernAliases { get; init; }

    public int ModernAliasesToCreate { get; init; }

    public int ExistingPrEvents { get; init; }

    public int PrEventsToCreate { get; init; }

    public int IdenticalSnapshotsIgnored { get; init; }

    public int SlowerTimesIgnored { get; init; }

    public int PrEventsImported { get; internal set; }

    internal List<ModernAliasPlan> AliasPlans { get; init; } = [];

    internal List<HistoricalEventPlan> EventPlans { get; init; } = [];
}

public sealed record MkddSnapshotHistoryMigrationProgress(
    int ImportedEvents,
    int TotalEvents,
    int ProcessedSeries,
    int TotalSeries);

internal sealed record ModernAliasPlan(
    int PlayerId,
    string PlayerName,
    string NormalizedName,
    int? ExistingAliasId);

internal sealed record HistoricalEventPlan(
    int PlayerId,
    int CourseId,
    TimeSpan Time,
    DateOnly RecordDate,
    DateTimeOffset FirstSeenAt,
    int SequenceNumber,
    TimeSpan? PreviousTime,
    TimeSpan? Improvement,
    int OccurrenceCount,
    string ExternalKey);
