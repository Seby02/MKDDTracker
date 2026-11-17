namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class HistoricalImportExecutionResult
{
    public int InputRecordsCount { get; set; }

    public int InvalidRecordsCount { get; set; }

    public int CoursesCreatedCount { get; set; }

    public int AliasesCreatedCount { get; set; }

    public int AliasesMatchedToPlayersCount { get; set; }

    public int AliasesWithoutPlayerCount { get; set; }

    public int PerformancesExistingCount { get; set; }

    public int PerformancesCreatedCount { get; set; }

    public int SourcesCreatedCount { get; set; }

    public bool TransactionCommitted { get; set; }

    public List<string> Errors { get; set; } = [];
}