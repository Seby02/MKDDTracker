namespace MKDDTracker.Scraper.Data.Entities;

public sealed class MkddPlayer
{
    public int Id { get; set; }

    public int SitePlayerId { get; set; }

    public required string Name { get; set; }

    public string? Country { get; set; }

    public ICollection<MkddPerformanceEntity> Performances { get; set; } = [];
}