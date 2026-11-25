namespace MKDDTracker.Scraper.Services.Database;

public enum HistoricalDatabaseHealthStatus
{
    Healthy,
    Warning,
    Error
}

public enum DatabaseCheckState
{
    Passed,
    Warning,
    Error,
    Informational
}

public sealed record HistoricalDatabaseCheckItem(
    string Name,
    DatabaseCheckState State,
    string Details);

public sealed record HistoricalDatabaseStatistics(
    long HistoricalCourseCount,
    long HistoricalAliasCount,
    long LinkedModernPlayerCount,
    long HistoricalPerformanceCount,
    long SourceCount,
    DateTimeOffset? FirstSeenAt,
    DateTimeOffset? LastSeenAt,
    long YahooGroupsCount,
    long MkddTrackerCount,
    long ThreeLapCount,
    long FlapCount,
    long UnknownTimeTypeCount,
    long FiftyHzCount,
    long SixtyHzCount,
    long UnknownFrequencyCount);

public sealed record HistoricalDatabaseIntegrityStatistics(
    long PerformancesWithoutAlias,
    long PerformancesWithoutCourse,
    long EmptyExternalKeys,
    long DuplicateExternalKeyGroups,
    long NonPositiveTimes,
    long InvalidTimeTypes,
    long InvalidFrequencies,
    long ImplausibleFirstSeenDates,
    long NegativeApprovedImprovements,
    long IncoherentApprovedPrMetadata,
    long MatchedAliasesWithoutPlayer,
    long AliasesPointingToMissingPlayers,
    long UnresolvedAliasesWithoutPlayer);

public sealed record MkddHistoricalDatabaseCheckResult(
    HistoricalDatabaseHealthStatus Status,
    bool DatabaseAccessible,
    IReadOnlyList<HistoricalDatabaseCheckItem> Checks,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> PendingMigrations,
    HistoricalDatabaseStatistics Historical,
    HistoricalDatabaseIntegrityStatistics Integrity);
