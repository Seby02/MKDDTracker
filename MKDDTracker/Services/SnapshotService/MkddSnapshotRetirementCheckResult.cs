namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotRetirementCheckResult
{
    public long SnapshotCount { get; init; }

    public long LegacyPerformanceCount { get; init; }

    public int SnapshotPerformancesAnalyzed { get; init; }

    public int PlayerCourseSeriesAnalyzed { get; init; }

    public int ExistingPrEvents { get; init; }

    public int PrEventsToCreate { get; init; }

    public int ExistingModernAliases { get; init; }

    public int ModernAliasesToCreate { get; init; }

    public bool IsReadyForRetirement =>
        PrEventsToCreate == 0 &&
        ModernAliasesToCreate == 0;
}