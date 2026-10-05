namespace MKDDTracker.Scraper.Data.Entities;

public sealed class MkddSnapshot
{
    public int Id { get; set; }

    public int CourseId { get; set; }

    public MkddCourse Course { get; set; } = null!;

    public DateTime CapturedAt { get; set; }

    public ICollection<MkddPerformanceEntity> Performances { get; set; } = [];
}