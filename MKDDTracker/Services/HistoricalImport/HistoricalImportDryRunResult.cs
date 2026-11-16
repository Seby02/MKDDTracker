namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class HistoricalImportDryRunResult
{
    public int InputRecordsCount { get; set; }

    public int InvalidRecordsCount { get; set; }

    public int CoursesAlreadyExistingCount { get; set; }

    public int CoursesToCreateCount { get; set; }

    public int AliasesAlreadyExistingCount { get; set; }

    public int AliasesToCreateCount { get; set; }

    public int AliasesMatchedToPlayersCount { get; set; }

    public int AliasesWithoutPlayerCount { get; set; }

    public int PerformancesAlreadyExistingCount { get; set; }

    public int PerformancesToCreateCount { get; set; }

    public int SourcesToCreateCount { get; set; }

    public bool HistoricalSchemaExists { get; set; }

    public List<string> CoursesToCreate { get; set; } = [];

    public List<string> Errors { get; set; } = [];
}