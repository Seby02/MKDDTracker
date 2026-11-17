using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotHistoryMigrationService
{
    private const int BatchSize = 500;
    private const string SourceSystem = "MKDDTracker";
    private const string TimeType = "ThreeLap";

    private readonly MkddDbContext _db;

    public MkddSnapshotHistoryMigrationService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<MkddSnapshotHistoryMigrationResult> AnalyzeAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshotPerformances = await _db.Performances
            .AsNoTracking()
            .Select(performance => new SnapshotPerformanceData(
                performance.PlayerId,
                performance.Player.Name,
                performance.Snapshot.CourseId,
                performance.Snapshot.RankingDate,
                performance.RecordDate,
                performance.Time))
            .ToListAsync(cancellationToken);

        List<ModernAliasData> modernAliases = await _db.HistoricalPlayerAliases
            .AsNoTracking()
            .Where(alias => alias.SourceSystem == SourceSystem)
            .Select(alias => new ModernAliasData(
                alias.Id,
                alias.PlayerId,
                alias.NormalizedExternalName))
            .ToListAsync(cancellationToken);

        HashSet<string> existingKeys = (await _db.HistoricalPerformances
                .AsNoTracking()
                .Where(performance => performance.SourceSystem == SourceSystem)
                .Select(performance => performance.ExternalKey)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<ModernAliasPlan> aliasPlans = snapshotPerformances
            .GroupBy(performance => performance.PlayerId)
            .Select(group =>
            {
                SnapshotPerformanceData representative = group
                    .OrderBy(item => item.PlayerName, StringComparer.OrdinalIgnoreCase)
                    .First();
                ModernAliasData? alias = modernAliases
                    .FirstOrDefault(item => item.PlayerId == group.Key)
                    ?? modernAliases.FirstOrDefault(item =>
                        string.Equals(
                            item.NormalizedExternalName,
                            NormalizeName(representative.PlayerName),
                            StringComparison.OrdinalIgnoreCase));

                return new ModernAliasPlan(
                    group.Key,
                    representative.PlayerName,
                    NormalizeName(representative.PlayerName),
                    alias?.Id);
            })
            .ToList();

        List<HistoricalEventPlan> eventPlans = [];
        int identicalIgnored = 0;
        int slowerIgnored = 0;

        foreach (IGrouping<PlayerCourseKey, SnapshotPerformanceData> series in
                 snapshotPerformances.GroupBy(item =>
                     new PlayerCourseKey(item.PlayerId, item.CourseId)))
        {
            List<SnapshotPerformanceData> chronological = series
                .OrderBy(item => item.SnapshotDate)
                .ThenBy(item => item.RecordDate)
                .ToList();
            Dictionary<TimeSpan, int> occurrenceCounts = chronological
                .GroupBy(item => item.Time)
                .ToDictionary(group => group.Key, group => group.Count());

            TimeSpan? bestTime = null;
            int sequenceNumber = 0;

            foreach (SnapshotPerformanceData performance in chronological)
            {
                if (bestTime is not null && performance.Time == bestTime.Value)
                {
                    identicalIgnored++;
                    continue;
                }

                if (bestTime is not null && performance.Time > bestTime.Value)
                {
                    slowerIgnored++;
                    continue;
                }

                TimeSpan? previousTime = bestTime;
                sequenceNumber++;
                DateTimeOffset firstSeenAt = new(
                    performance.RecordDate.ToDateTime(new TimeOnly(12, 0)),
                    TimeSpan.Zero);
                string externalKey = BuildExternalKey(
                    performance.PlayerId,
                    performance.CourseId,
                    performance.Time,
                    performance.RecordDate);

                eventPlans.Add(new HistoricalEventPlan(
                    performance.PlayerId,
                    performance.CourseId,
                    performance.Time,
                    performance.RecordDate,
                    firstSeenAt,
                    sequenceNumber,
                    previousTime,
                    previousTime is null ? null : previousTime.Value - performance.Time,
                    occurrenceCounts[performance.Time],
                    externalKey));

                bestTime = performance.Time;
            }
        }

        int existingEventCount = eventPlans.Count(plan =>
            existingKeys.Contains(plan.ExternalKey));
        List<HistoricalEventPlan> newEventPlans = eventPlans
            .Where(plan => !existingKeys.Contains(plan.ExternalKey))
            .ToList();

        return new MkddSnapshotHistoryMigrationResult
        {
            EarliestRecordDate = snapshotPerformances.Count == 0
                ? null
                : snapshotPerformances.Min(item => item.RecordDate),
            LatestRecordDate = snapshotPerformances.Count == 0
                ? null
                : snapshotPerformances.Max(item => item.RecordDate),
            EarliestRankingDate = snapshotPerformances.Count == 0
                ? null
                : snapshotPerformances.Min(item => item.SnapshotDate),
            LatestRankingDate = snapshotPerformances.Count == 0
                ? null
                : snapshotPerformances.Max(item => item.SnapshotDate),
            SnapshotPerformancesAnalyzed = snapshotPerformances.Count,
            PlayerCourseSeriesAnalyzed = snapshotPerformances
                .Select(item => new PlayerCourseKey(item.PlayerId, item.CourseId))
                .Distinct()
                .Count(),
            ExistingModernAliases = aliasPlans.Count(plan => plan.ExistingAliasId is not null),
            ModernAliasesToCreate = aliasPlans.Count(plan => plan.ExistingAliasId is null),
            ExistingPrEvents = existingEventCount,
            PrEventsToCreate = newEventPlans.Count,
            IdenticalSnapshotsIgnored = identicalIgnored,
            SlowerTimesIgnored = slowerIgnored,
            AliasPlans = aliasPlans,
            EventPlans = newEventPlans
        };
    }

    public async Task<MkddSnapshotHistoryMigrationResult> ImportAsync(
        Action<MkddSnapshotHistoryMigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        MkddSnapshotHistoryMigrationResult result = await AnalyzeAsync(cancellationToken);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            List<MkddHistoricalPlayerAlias> existingAliases = await _db.HistoricalPlayerAliases
                .Where(alias => alias.SourceSystem == SourceSystem)
                .ToListAsync(cancellationToken);
            Dictionary<int, MkddHistoricalPlayerAlias> aliasesByPlayerId = existingAliases
                .Where(alias => alias.PlayerId is not null)
                .GroupBy(alias => alias.PlayerId!.Value)
                .ToDictionary(group => group.Key, group => group.First());
            Dictionary<string, MkddHistoricalPlayerAlias> aliasesByNormalizedName = existingAliases
                .GroupBy(alias => alias.NormalizedExternalName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            foreach (ModernAliasPlan plan in result.AliasPlans)
            {
                MkddHistoricalPlayerAlias alias;
                if (!aliasesByPlayerId.TryGetValue(plan.PlayerId, out alias!))
                {
                    if (!aliasesByNormalizedName.TryGetValue(plan.NormalizedName, out alias!))
                    {
                        alias = new MkddHistoricalPlayerAlias
                        {
                            SourceSystem = SourceSystem,
                            ExternalName = plan.PlayerName,
                            NormalizedExternalName = plan.NormalizedName,
                            PlayerId = plan.PlayerId,
                            Confidence = 100,
                            ResolutionStatus = "Matched"
                        };
                        _db.HistoricalPlayerAliases.Add(alias);
                        aliasesByNormalizedName[plan.NormalizedName] = alias;
                    }
                }

                alias.ExternalName = plan.PlayerName;
                alias.NormalizedExternalName = plan.NormalizedName;
                alias.PlayerId = plan.PlayerId;
                alias.Confidence = 100;
                alias.ResolutionStatus = "Matched";
                aliasesByPlayerId[plan.PlayerId] = alias;
            }

            await _db.SaveChangesAsync(cancellationToken);

            int imported = 0;
            for (int offset = 0; offset < result.EventPlans.Count; offset += BatchSize)
            {
                List<HistoricalEventPlan> batch = result.EventPlans
                    .Skip(offset)
                    .Take(BatchSize)
                    .ToList();

                foreach (HistoricalEventPlan plan in batch)
                {
                    _db.HistoricalPerformances.Add(new MkddHistoricalPerformance
                    {
                        ExternalKey = plan.ExternalKey,
                        HistoricalPlayerAliasId = aliasesByPlayerId[plan.PlayerId].Id,
                        CourseId = plan.CourseId,
                        TimeType = TimeType,
                        Frequency = null,
                        Time = plan.Time,
                        FirstSeenAt = plan.FirstSeenAt,
                        DateSource = "MKDDTrackerRecordDate",
                        SequenceNumber = plan.SequenceNumber,
                        PreviousTime = plan.PreviousTime,
                        Improvement = plan.Improvement,
                        OccurrenceCount = plan.OccurrenceCount,
                        SourceSystem = SourceSystem,
                        ImportStatus = "Approved"
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
                imported += batch.Count;
                int processedSeries = result.EventPlans
                    .Take(imported)
                    .Select(plan => new PlayerCourseKey(plan.PlayerId, plan.CourseId))
                    .Distinct()
                    .Count();
                progress?.Invoke(new MkddSnapshotHistoryMigrationProgress(
                    imported,
                    result.EventPlans.Count,
                    processedSeries,
                    result.PlayerCourseSeriesAnalyzed));
            }

            await transaction.CommitAsync(cancellationToken);
            result.PrEventsImported = imported;
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string BuildExternalKey(
        int playerId,
        int courseId,
        TimeSpan time,
        DateOnly recordDate)
    {
        string rawKey = string.Join(
            "|",
            SourceSystem,
            playerId.ToString(CultureInfo.InvariantCulture),
            courseId.ToString(CultureInfo.InvariantCulture),
            TimeType,
            "UNKNOWN",
            ((long)time.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
            recordDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
    }

    private static string NormalizeName(string value)
    {
        string normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        return Regex.Replace(normalized, @"\s+", " ").ToLowerInvariant();
    }

    private sealed record SnapshotPerformanceData(
        int PlayerId,
        string PlayerName,
        int CourseId,
        DateOnly SnapshotDate,
        DateOnly RecordDate,
        TimeSpan Time);

    private sealed record ModernAliasData(
        int Id,
        int? PlayerId,
        string NormalizedExternalName);

    private readonly record struct PlayerCourseKey(int PlayerId, int CourseId);
}
