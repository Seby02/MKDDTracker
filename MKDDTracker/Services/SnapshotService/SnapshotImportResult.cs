using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public enum SnapshotImportStatus
{
    Created,
    AlreadyExists,
    Replaced,
    Failed
}

public sealed record SnapshotImportResult(
    SnapshotImportStatus Status,
    int ImportedPerformances,
    MkddSnapshot? Snapshot,
    string? Error);
