namespace MKDDTracker.Scraper.Services.HistoricalImport;

public enum ModernPrDetectionStatus
{
    FirstPr,
    Improvement,
    Unchanged,
    Slower,
    Existing,
    Rejected
}

public enum TimeTypeValidationStatus
{
    NotChecked,
    Consistent,
    Suspicious
}

public sealed record ModernImportPerformanceOutcome(
    string PlayerName,
    string CourseName,
    TimeSpan Time,
    string TimeType,
    int? Frequency,
    ModernPrDetectionStatus Status,
    TimeSpan? PreviousTime,
    TimeSpan? Improvement,
    int SequenceNumber,
    string? DateSource,
    string? Error);

public sealed record MkddHistoricalModernImportResult(
    int PerformancesAnalyzed,
    int SeriesAnalyzed,
    int ExistingAliases,
    int AliasesToCreate,
    IReadOnlyList<ModernImportPerformanceOutcome> Outcomes,
    IReadOnlyList<string> Errors,
    int EventsImported = 0,
    bool ImportCompleted = false)
{
    public TimeTypeValidationStatus TimeTypeValidation { get; init; } = TimeTypeValidationStatus.NotChecked;

    public string? TimeTypeValidationReason { get; init; }

    public int FirstPrsDetected =>
        Outcomes.Count(outcome => outcome.Status == ModernPrDetectionStatus.FirstPr);

    public int ImprovementsDetected =>
        Outcomes.Count(outcome => outcome.Status == ModernPrDetectionStatus.Improvement);

    public int IdenticalTimesIgnored =>
        Outcomes.Count(outcome => outcome.Status == ModernPrDetectionStatus.Unchanged);

    public int SlowerTimesIgnored =>
        Outcomes.Count(outcome => outcome.Status == ModernPrDetectionStatus.Slower);

    public int ExistingEvents =>
        Outcomes.Count(outcome => outcome.Status == ModernPrDetectionStatus.Existing);

    public int EventsToCreate => FirstPrsDetected + ImprovementsDetected;

    public int RejectedCount =>
        Outcomes.Count(outcome => outcome.Status == ModernPrDetectionStatus.Rejected);
}
