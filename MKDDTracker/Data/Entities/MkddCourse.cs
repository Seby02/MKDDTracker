namespace MKDDTracker.Scraper.Data.Entities;

public sealed class MkddCourse
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public ICollection<MkddHistoricalPerformance> HistoricalPerformances { get; set; } = [];
}