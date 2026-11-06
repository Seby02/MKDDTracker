namespace MKDDTracker.Scraper.Data.Entities;

public sealed class MkddPerformanceEntity
{
    public int Id { get; set; }

    public int SnapshotId { get; set; }

    public MkddSnapshot Snapshot { get; set; } = null!;

    public int PlayerId { get; set; }

    public MkddPlayer Player { get; set; } = null!;

    public int Rank { get; set; }

    public TimeSpan Time { get; set; }

    public string? Standard { get; set; }

    public DateOnly RecordDate { get; set; }

    public string? VideoUrl { get; set; }
}