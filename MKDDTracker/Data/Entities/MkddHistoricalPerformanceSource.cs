namespace MKDDTracker.Scraper.Data.Entities;

public sealed class MkddHistoricalPerformanceSource
{
    public long Id { get; set; }

    public long HistoricalPerformanceId { get; set; }

    public MkddHistoricalPerformance HistoricalPerformance { get; set; } = null!;

    public long MessageId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? SourceFile { get; set; }

    public string? OriginalTime { get; set; }

    public string? SourceLine { get; set; }

    public int PlayerConfidence { get; set; }

    public int TimeTypeConfidence { get; set; }

    public required string ImportStatus { get; set; }
}