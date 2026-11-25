using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotRetirementCheckService
{
    private readonly MkddDbContext _db;
    private readonly MkddSnapshotHistoryMigrationService
        _migrationService;

    public MkddSnapshotRetirementCheckService(
        MkddDbContext db)
    {
        _db = db;

        _migrationService =
            new MkddSnapshotHistoryMigrationService(db);
    }

    public async Task<MkddSnapshotRetirementCheckResult>
        CheckAsync(
            CancellationToken cancellationToken = default)
    {
        long snapshotCount =
            await _db.Snapshots
                .AsNoTracking()
                .LongCountAsync(
                    cancellationToken);

        long legacyPerformanceCount =
            await _db.Performances
                .AsNoTracking()
                .LongCountAsync(
                    cancellationToken);

        MkddSnapshotHistoryMigrationResult migration =
            await _migrationService.AnalyzeAsync(
                cancellationToken);

        return new MkddSnapshotRetirementCheckResult
        {
            SnapshotCount =
                snapshotCount,

            LegacyPerformanceCount =
                legacyPerformanceCount,

            SnapshotPerformancesAnalyzed =
                migration.SnapshotPerformancesAnalyzed,

            PlayerCourseSeriesAnalyzed =
                migration.PlayerCourseSeriesAnalyzed,

            ExistingPrEvents =
                migration.ExistingPrEvents,

            PrEventsToCreate =
                migration.PrEventsToCreate,

            ExistingModernAliases =
                migration.ExistingModernAliases,

            ModernAliasesToCreate =
                migration.ModernAliasesToCreate
        };
    }
}