namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class YahooHistoricalRecord
{
    public string PlayerName { get; set; } =
        string.Empty;

    public string TrackCode { get; set; } =
        string.Empty;

    public string TimeType { get; set; } =
        string.Empty;

    public int? Frequency { get; set; }

    public long TimeMilliseconds { get; set; }

    public string FormattedTime { get; set; } =
        string.Empty;

    public DateTimeOffset FirstSeenAt { get; set; }

    public string DateSource { get; set; } =
        string.Empty;

    public int SequenceNumber { get; set; }

    public long? PreviousTimeMilliseconds { get; set; }

    public long? ImprovementMilliseconds { get; set; }

    public string ImportStatus { get; set; } =
        string.Empty;

    public List<string> ImportReasons { get; set; } = [];

    public List<string> ValidationWarnings { get; set; } = [];

    public string SourceSystem { get; set; } =
        string.Empty;

    public int OccurrenceCount { get; set; }

    public List<YahooHistoricalRecordSource> Sources { get; set; } = [];
}

public sealed class YahooHistoricalRecordSource
{
    public long MessageId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? SourceFile { get; set; }

    public string? OriginalTime { get; set; }

    public string? SourceLine { get; set; }

    public int PlayerConfidence { get; set; }

    public int TimeTypeConfidence { get; set; }

    public string ImportStatus { get; set; } =
        string.Empty;
}