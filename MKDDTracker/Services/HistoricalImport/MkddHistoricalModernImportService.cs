using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;
using MKDDTracker.Scraper.Services.EvolutionService;

namespace MKDDTracker.Scraper.Services.HistoricalImport;

public sealed class MkddHistoricalModernImportService
{
    private const string SourceSystem = "MKDDTracker";

    private readonly IDbContextFactory<MkddDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;
    private readonly MkddTimeTypePlausibilityOptions _timeTypePlausibilityOptions;

    public MkddHistoricalModernImportService(
        IDbContextFactory<MkddDbContext> dbFactory,
        TimeProvider? timeProvider = null,
        MkddTimeTypePlausibilityOptions? timeTypePlausibilityOptions = null)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _timeTypePlausibilityOptions = timeTypePlausibilityOptions ?? new MkddTimeTypePlausibilityOptions();
        _timeTypePlausibilityOptions.Validate();
    }

    public async Task<MkddHistoricalModernImportResult> AnalyzeAsync(
        string courseName,
        IReadOnlyList<MkddPerformance?>? performances,
        DateTimeOffset? observationAt = null,
        string? fallbackTimeType = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        ImportPlan plan = await AnalyzeCoreAsync(
            db,
            courseName,
            performances,
            observationAt,
            fallbackTimeType,
            _timeProvider.GetUtcNow(),
            cancellationToken);
        return plan.Result;
    }

    public async Task<MkddHistoricalModernImportResult> ImportAsync(
        string courseName,
        IReadOnlyList<MkddPerformance?>? performances,
        DateTimeOffset? observationAt = null,
        string? fallbackTimeType = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        IDbContextTransaction? transaction = null;
        ImportPlan? plan = null;

        try
        {
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            plan = await AnalyzeCoreAsync(
                db,
                courseName,
                performances,
                observationAt,
                fallbackTimeType,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            if (plan.Result.Errors.Count > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return plan.Result;
            }

            List<PreparedPerformance> toImport = plan.Performances
                .Where(item => item.Status is ModernPrDetectionStatus.FirstPr or ModernPrDetectionStatus.Improvement)
                .ToList();

            if (toImport.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return plan.Result with { ImportCompleted = true };
            }

            MkddCourse? course = await db.Courses
                .SingleOrDefaultAsync(item => item.Name == plan.CourseName, cancellationToken);

            if (course is null)
            {
                course = new MkddCourse { Name = plan.CourseName };
                db.Courses.Add(course);
            }

            int[] sitePlayerIds = toImport
                .Select(item => item.Source.SitePlayerId)
                .Distinct()
                .ToArray();
            List<MkddPlayer> players = await db.Players
                .Where(item => sitePlayerIds.Contains(item.SitePlayerId))
                .ToListAsync(cancellationToken);
            Dictionary<int, MkddPlayer> playersBySiteId = players
                .ToDictionary(item => item.SitePlayerId);

            foreach (PreparedPerformance item in toImport)
            {
                if (!playersBySiteId.ContainsKey(item.Source.SitePlayerId))
                {
                    var player = new MkddPlayer
                    {
                        SitePlayerId = item.Source.SitePlayerId,
                        Name = item.PlayerName,
                        Country = item.Source.Country
                    };
                    playersBySiteId.Add(player.SitePlayerId, player);
                    db.Players.Add(player);
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            string[] normalizedNames = toImport
                .Select(item => HistoricalPlayerNameNormalizer.NormalizePlayerName(
                    playersBySiteId[item.Source.SitePlayerId].Name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            List<MkddHistoricalPlayerAlias> aliases = await db.HistoricalPlayerAliases
                .Where(alias => alias.SourceSystem == SourceSystem &&
                                normalizedNames.Contains(alias.NormalizedExternalName))
                .ToListAsync(cancellationToken);
            Dictionary<string, MkddHistoricalPlayerAlias> aliasesByName = aliases
                .ToDictionary(alias => alias.NormalizedExternalName, StringComparer.Ordinal);
            Dictionary<int, MkddHistoricalPlayerAlias> aliasesBySitePlayerId = [];

            foreach (PreparedPerformance item in toImport)
            {
                MkddPlayer player = playersBySiteId[item.Source.SitePlayerId];
                string normalizedName = HistoricalPlayerNameNormalizer
                    .NormalizePlayerName(player.Name);

                if (!aliasesByName.TryGetValue(normalizedName, out MkddHistoricalPlayerAlias? alias))
                {
                    alias = new MkddHistoricalPlayerAlias
                    {
                        SourceSystem = SourceSystem,
                        ExternalName = player.Name,
                        NormalizedExternalName = normalizedName,
                        PlayerId = player.Id,
                        Confidence = 100,
                        ResolutionStatus = "Matched"
                    };
                    db.HistoricalPlayerAliases.Add(alias);
                    aliasesByName.Add(normalizedName, alias);
                }
                else if (alias.PlayerId is not null && alias.PlayerId != player.Id)
                {
                    throw new InvalidOperationException(
                        $"L'alias '{player.Name}' est déjà associé à un autre joueur.");
                }

                alias.ExternalName = player.Name;
                alias.NormalizedExternalName = normalizedName;
                alias.PlayerId = player.Id;
                alias.Confidence = 100;
                alias.ResolutionStatus = "Matched";
                aliasesBySitePlayerId[item.Source.SitePlayerId] = alias;
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (PreparedPerformance item in toImport)
            {
                MkddHistoricalPlayerAlias alias = aliasesBySitePlayerId[item.Source.SitePlayerId];
                db.HistoricalPerformances.Add(new MkddHistoricalPerformance
                {
                    ExternalKey = item.ExternalKey,
                    HistoricalPlayerAliasId = alias.Id,
                    CourseId = course.Id,
                    TimeType = item.TimeType,
                    Frequency = item.Frequency,
                    Time = item.Source.Time,
                    FirstSeenAt = item.FirstSeenAt,
                    DateSource = item.DateSource,
                    PreviousTime = item.PreviousTime,
                    Improvement = item.Improvement,
                    SequenceNumber = item.SequenceNumber,
                    OccurrenceCount = 1,
                    SourceSystem = SourceSystem,
                    ImportStatus = "Approved"
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return plan.Result with
            {
                EventsImported = toImport.Count,
                ImportCompleted = true
            };
        }
        catch (Exception exception)
        {
            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                }
            }

            db.ChangeTracker.Clear();
            string error = $"L'import historique moderne a échoué : {GetInnermostMessage(exception)}";
            if (plan is null)
            {
                return new MkddHistoricalModernImportResult(
                    performances?.Count ?? 0,
                    0,
                    0,
                    0,
                    [],
                    [error]);
            }

            return plan.Result with
            {
                Errors = plan.Result.Errors.Append(error).ToList(),
                ImportCompleted = false
            };
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task<ImportPlan> AnalyzeCoreAsync(
        MkddDbContext db,
        string courseName,
        IReadOnlyList<MkddPerformance?>? performances,
        DateTimeOffset? observationAt,
        string? fallbackTimeType,
        DateTimeOffset importedAt,
        CancellationToken cancellationToken)
    {
        string normalizedCourseName = courseName?.Trim() ?? string.Empty;
        List<string> errors = [];
        List<PreparedPerformance> prepared = [];
        int inputCount = performances?.Count ?? 0;

        if (string.IsNullOrWhiteSpace(normalizedCourseName))
            errors.Add("Le nom du circuit est obligatoire.");

        if (performances is null)
            errors.Add("La liste des performances parsees est absente.");
        else if (performances.Count == 0)
            errors.Add("Aucune performance parsee.");

        if (performances is null || performances.Count == 0)
        {
            return new ImportPlan(normalizedCourseName, [], new MkddHistoricalModernImportResult(
                inputCount, 0, 0, 0, [], errors));
        }

        const string nullPerformanceError = "Une performance parsee est nulle.";
        List<MkddPerformance> parsedPerformances = [];
        foreach (MkddPerformance? performance in performances)
        {
            if (performance is null)
                errors.Add("Une ou plusieurs performances parsees sont nulles.");
            else
                parsedPerformances.Add(performance);
        }

        if (errors.Count > 0)
        {
            List<string> rejectionErrors = [.. errors];
            List<ModernImportPerformanceOutcome> rejected = [];
            foreach (MkddPerformance? item in performances)
            {
                string reason = item is null
                    ? nullPerformanceError
                    : ValidateTimeType(item.TimeType, fallbackTimeType, item.SitePlayerId, out _) ?? errors[0];
                if (!rejectionErrors.Contains(reason))
                    rejectionErrors.Add(reason);
                rejected.Add(CreateRejectedOutcome(item, normalizedCourseName, reason));
            }

            return new ImportPlan(normalizedCourseName, [], new MkddHistoricalModernImportResult(
                inputCount, 0, 0, 0, rejected, rejectionErrors));
        }

        HashSet<int> duplicateSitePlayerIds = parsedPerformances
            .GroupBy(item => item.SitePlayerId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        int[] sourcePlayerIds = parsedPerformances
            .Where(item => item.SitePlayerId > 0)
            .Select(item => item.SitePlayerId)
            .Distinct()
            .ToArray();

        Dictionary<int, ExistingPlayer> existingPlayers = await db.Players
            .AsNoTracking()
            .Where(player => sourcePlayerIds.Contains(player.SitePlayerId))
            .Select(player => new ExistingPlayer(player.Id, player.SitePlayerId, player.Name))
            .ToDictionaryAsync(player => player.SitePlayerId, cancellationToken);

        MkddCourse? existingCourse = await db.Courses
            .AsNoTracking()
            .SingleOrDefaultAsync(course => course.Name == normalizedCourseName, cancellationToken);

        foreach (MkddPerformance source in parsedPerformances)
        {
            string? validationError = ValidatePerformance(
                source,
                duplicateSitePlayerIds,
                fallbackTimeType,
                out string timeType,
                out int? frequency);

            if (validationError is not null)
            {
                errors.Add(validationError);
                prepared.Add(PreparedPerformance.Rejected(
                    source,
                    normalizedCourseName,
                    timeType,
                    frequency,
                    validationError));
                continue;
            }

            ExistingPlayer? player = existingPlayers.GetValueOrDefault(source.SitePlayerId);
            string playerName = player?.Name ?? source.PlayerName.Trim();
            string normalizedPlayerName = HistoricalPlayerNameNormalizer
                .NormalizePlayerName(playerName);
            (DateTimeOffset firstSeenAt, string dateSource) = ResolveDate(
                source.RecordDate,
                observationAt,
                importedAt);

            prepared.Add(new PreparedPerformance
            {
                Source = source,
                PlayerName = playerName,
                CourseName = normalizedCourseName,
                NormalizedPlayerName = normalizedPlayerName,
                TimeType = timeType,
                Frequency = frequency,
                FirstSeenAt = firstSeenAt,
                DateSource = dateSource,
                ExistingPlayerId = player?.Id,
                ExistingAliasId = null,
                ExternalKey = BuildExternalKey(
                    source.SitePlayerId,
                    normalizedCourseName,
                    timeType,
                    frequency,
                    source.Time,
                    firstSeenAt,
                    dateSource),
                Status = ModernPrDetectionStatus.FirstPr
            });
        }

        TimeTypeValidationResult typeValidation = ValidateTimeTypePlausibility(prepared);
        if (typeValidation.Status == TimeTypeValidationStatus.Suspicious &&
            typeValidation.Reason is string suspiciousReason)
        {
            errors.Add(suspiciousReason);
            foreach (PreparedPerformance item in prepared.Where(
                         item => item.Status != ModernPrDetectionStatus.Rejected))
            {
                item.Status = ModernPrDetectionStatus.Rejected;
                item.Error = suspiciousReason;
            }
        }

        List<MkddHistoricalPlayerAlias> modernAliases = await db.HistoricalPlayerAliases
            .AsNoTracking()
            .Where(alias => alias.SourceSystem == SourceSystem)
            .ToListAsync(cancellationToken);
        Dictionary<string, MkddHistoricalPlayerAlias> aliasesByNormalizedName = modernAliases
            .GroupBy(alias => alias.NormalizedExternalName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (PreparedPerformance item in prepared.Where(item => item.Source is not null &&
                                                                     item.Status != ModernPrDetectionStatus.Rejected))
        {
            if (!aliasesByNormalizedName.TryGetValue(item.NormalizedPlayerName, out MkddHistoricalPlayerAlias? alias))
                continue;

            if (alias.PlayerId is not null && alias.PlayerId != item.ExistingPlayerId)
            {
                string error = $"L'alias '{item.PlayerName}' est déjà associé à un autre joueur.";
                item.Status = ModernPrDetectionStatus.Rejected;
                item.Error = error;
                errors.Add(error);
                continue;
            }

            item.ExistingAliasId = alias.Id;
        }

        List<PreparedPerformance> validPrepared = prepared
            .Where(item => item.Status != ModernPrDetectionStatus.Rejected)
            .ToList();
        int[] modernPlayerIds = validPrepared
            .Where(item => item.ExistingPlayerId.HasValue)
            .Select(item => item.ExistingPlayerId.GetValueOrDefault())
            .Distinct()
            .ToArray();

        List<ExistingHistory> existingHistory = [];
        if (existingCourse is not null && modernPlayerIds.Length > 0)
        {
            existingHistory = await db.HistoricalPerformances
                .AsNoTracking()
                .Where(performance => performance.CourseId == existingCourse.Id &&
                                      performance.HistoricalPlayerAlias.PlayerId.HasValue &&
                                      modernPlayerIds.Contains(performance.HistoricalPlayerAlias.PlayerId.Value))
                .Select(performance => new ExistingHistory(
                    performance.HistoricalPlayerAlias.PlayerId.GetValueOrDefault(),
                    performance.CourseId,
                    performance.TimeType,
                    performance.Frequency,
                    performance.Time,
                    performance.FirstSeenAt,
                    performance.SequenceNumber,
                    performance.SourceSystem))
                .ToListAsync(cancellationToken);
        }

        Dictionary<SeriesKey, HistorySummary> historyBySeries = BuildHistorySummaries(existingHistory);
        List<string> candidateKeys = validPrepared
            .Select(item => item.ExternalKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        HashSet<string> existingExternalKeys = candidateKeys.Count == 0
            ? []
            : (await db.HistoricalPerformances
                    .AsNoTracking()
                    .Where(performance => candidateKeys.Contains(performance.ExternalKey))
                    .Select(performance => performance.ExternalKey)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

        foreach (PreparedPerformance item in validPrepared)
        {
            HistorySummary? history = null;
            if (existingCourse is not null && item.ExistingPlayerId.HasValue)
            {
                historyBySeries.TryGetValue(
                    new SeriesKey(
                        item.ExistingPlayerId.Value,
                        existingCourse.Id,
                        item.TimeType,
                        item.Frequency),
                    out history);
            }

            item.PreviousTime = history?.BestTime;
            item.SequenceNumber = (history?.MaximumSequenceNumber ?? 0) + 1;

            if (existingExternalKeys.Contains(item.ExternalKey))
            {
                item.Status = ModernPrDetectionStatus.Existing;
            }
            else if (item.PreviousTime is null)
            {
                item.Status = ModernPrDetectionStatus.FirstPr;
            }
            else if (item.Source.Time < item.PreviousTime.Value)
            {
                item.Status = ModernPrDetectionStatus.Improvement;
                item.Improvement = item.PreviousTime.Value - item.Source.Time;
            }
            else if (item.Source.Time == item.PreviousTime.Value)
            {
                item.Status = ModernPrDetectionStatus.Unchanged;
            }
            else
            {
                item.Status = ModernPrDetectionStatus.Slower;
            }
        }

        List<PreparedPerformance> eventCandidates = validPrepared
            .Where(item => item.Status is ModernPrDetectionStatus.FirstPr or ModernPrDetectionStatus.Improvement)
            .ToList();
        int existingAliasCount = validPrepared
            .Where(item => item.ExistingAliasId.HasValue)
            .Select(item => item.NormalizedPlayerName)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int aliasesToCreate = eventCandidates
            .Where(item => !item.ExistingAliasId.HasValue)
            .Select(item => item.NormalizedPlayerName)
            .Distinct(StringComparer.Ordinal)
            .Count();

        List<ModernImportPerformanceOutcome> outcomes = prepared
            .Select(item => new ModernImportPerformanceOutcome(
                item.PlayerName,
                item.CourseName,
                item.Source.Time,
                item.TimeType,
                item.Frequency,
                item.Status,
                item.PreviousTime,
                item.Improvement,
                item.SequenceNumber,
                item.DateSource,
                item.Error))
            .ToList();
        int seriesCount = validPrepared
            .Select(item => new SeriesInputKey(
                item.Source.SitePlayerId,
                normalizedCourseName,
                item.TimeType,
                item.Frequency))
            .Distinct()
            .Count();

        return new ImportPlan(
            normalizedCourseName,
            prepared,
            new MkddHistoricalModernImportResult(
                inputCount,
                seriesCount,
                existingAliasCount,
                aliasesToCreate,
                outcomes,
                errors)
            {
                TimeTypeValidation = typeValidation.Status,
                TimeTypeValidationReason = typeValidation.Reason
            });
    }

    private TimeTypeValidationResult ValidateTimeTypePlausibility(
        IReadOnlyList<PreparedPerformance> performances)
    {
        bool evaluated = false;
        foreach (IGrouping<string, PreparedPerformance> group in performances
                     .Where(item => item.Status != ModernPrDetectionStatus.Rejected && item.Source.Time > TimeSpan.Zero)
                     .GroupBy(item => item.TimeType))
        {
            int sampleSize = group.Count();
            if (sampleSize < _timeTypePlausibilityOptions.MinimumSampleSize)
                continue;

            evaluated = true;
            int suspiciousCount;
            string condition;
            int thresholdSeconds;
            if (group.Key == "Flap")
            {
                thresholdSeconds = _timeTypePlausibilityOptions.FlapSuspiciousThresholdSeconds;
                suspiciousCount = group.Count(item => item.Source.Time > TimeSpan.FromSeconds(thresholdSeconds));
                condition = $"dépassent {thresholdSeconds} s";
            }
            else
            {
                thresholdSeconds = _timeTypePlausibilityOptions.ThreeLapSuspiciousThresholdSeconds;
                suspiciousCount = group.Count(item => item.Source.Time < TimeSpan.FromSeconds(thresholdSeconds));
                condition = $"sont inférieurs à {thresholdSeconds} s";
            }

            double suspiciousRatio = suspiciousCount / (double)sampleSize;
            if (suspiciousRatio >= _timeTypePlausibilityOptions.SuspiciousRatio)
            {
                string reason =
                    $"SuspiciousTimeType: {suspiciousCount}/{sampleSize} chronos {group.Key} {condition} ; " +
                    "vérifie le type déclaré avant l’import.";
                return new TimeTypeValidationResult(TimeTypeValidationStatus.Suspicious, reason);
            }
        }

        return new TimeTypeValidationResult(
            evaluated ? TimeTypeValidationStatus.Consistent : TimeTypeValidationStatus.NotChecked,
            null);
    }

    private static Dictionary<SeriesKey, HistorySummary> BuildHistorySummaries(
        IReadOnlyList<ExistingHistory> history)
    {
        return history
            .GroupBy(item => new SeriesKey(
                item.PlayerId,
                item.CourseId,
                item.TimeType,
                item.Frequency))
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    IReadOnlyList<ConsolidatedHistoricalPerformance<ExistingHistory>> consolidated =
                        HistoricalPerformanceConsolidator.Consolidate(
                            group,
                            item => new HistoricalPerformanceDeduplicationKey(
                                $"player:{item.PlayerId.ToString(CultureInfo.InvariantCulture)}",
                                item.CourseId,
                                item.TimeType,
                                item.Time,
                                DateOnly.FromDateTime(item.FirstSeenAt.UtcDateTime)),
                            item => item.Frequency,
                            item => item.SourceSystem,
                            item => item.FirstSeenAt,
                            item => item.SequenceNumber,
                            preserveFrequencyCategories: true);

                    return new HistorySummary(
                        consolidated.Min(item => item.Preferred.Time),
                        group.Max(item => item.SequenceNumber));
                });
    }

    private static string? ValidatePerformance(
        MkddPerformance performance,
        IReadOnlySet<int> duplicateSitePlayerIds,
        string? fallbackTimeType,
        out string timeType,
        out int? frequency)
    {
        string? error = null;
        frequency = performance.Frequency;
        string? timeTypeError = ValidateTimeType(
            performance.TimeType,
            fallbackTimeType,
            performance.SitePlayerId,
            out timeType);
        if (timeTypeError is not null)
            return timeTypeError;

        if (performance.SitePlayerId <= 0)
            error = "Chaque performance doit contenir l'identifiant joueur fourni par la source.";
        else if (duplicateSitePlayerIds.Contains(performance.SitePlayerId))
            error = $"Le joueur {performance.SitePlayerId} apparaît plusieurs fois dans le classement.";
        else if (string.IsNullOrWhiteSpace(performance.PlayerName))
            error = $"Le joueur {performance.SitePlayerId} n'a pas de nom.";
        else if (performance.Time <= TimeSpan.Zero)
            error = $"Le chrono du joueur {performance.SitePlayerId} est invalide.";
        else if (performance.Frequency is not null and not 50 and not 60)
            error = $"La fréquence du joueur {performance.SitePlayerId} doit être 50, 60 ou inconnue.";

        if (error is not null)
            return error;

        return null;
    }

    private static string? ValidateTimeType(
        string? parsedTimeType,
        string? fallbackTimeType,
        int sitePlayerId,
        out string timeType)
    {
        timeType = string.Empty;
        if (string.IsNullOrWhiteSpace(parsedTimeType) && string.IsNullOrWhiteSpace(fallbackTimeType))
            return "MissingTimeType";

        string? resolved = ResolveTimeType(parsedTimeType, fallbackTimeType);
        if (resolved is null)
            return $"InvalidTimeType: le type de chrono du joueur {sitePlayerId} doit être ThreeLap ou Flap.";

        timeType = resolved;
        return null;
    }

    private static string? ResolveTimeType(string? parsedTimeType, string? fallbackTimeType)
    {
        string? value = string.IsNullOrWhiteSpace(parsedTimeType)
            ? fallbackTimeType
            : parsedTimeType;

        return value is "ThreeLap" or "Flap" ? value : null;
    }

    private static (DateTimeOffset FirstSeenAt, string DateSource) ResolveDate(
        DateOnly recordDate,
        DateTimeOffset? observationAt,
        DateTimeOffset importedAt)
    {
        if (recordDate != default)
        {
            return (
                new DateTimeOffset(recordDate.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero),
                "RecordDate");
        }

        if (observationAt is not null)
            return (observationAt.Value.ToUniversalTime(), "ObservationDate");

        return (importedAt.ToUniversalTime(), "ImportDate");
    }

    private static string BuildExternalKey(
        int sitePlayerId,
        string courseName,
        string timeType,
        int? frequency,
        TimeSpan time,
        DateTimeOffset firstSeenAt,
        string dateSource)
    {
        string[] parts =
        [
            SourceSystem,
            $"site-player:{sitePlayerId.ToString(CultureInfo.InvariantCulture)}",
            HistoricalPlayerNameNormalizer.NormalizeText(courseName),
            timeType,
            frequency?.ToString(CultureInfo.InvariantCulture) ?? "<NO_FREQUENCY>",
            time.Ticks.ToString(CultureInfo.InvariantCulture)
        ];

        string rawKey = dateSource == "ImportDate"
            ? string.Join("|", parts)
            : string.Join(
                "|",
                parts.Append(dateSource)
                    .Append(firstSeenAt.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture)));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey)));
    }

    private static ModernImportPerformanceOutcome CreateRejectedOutcome(
        MkddPerformance? performance,
        string courseName,
        string error) =>
        new(
            performance?.PlayerName ?? string.Empty,
            courseName,
            performance?.Time ?? TimeSpan.Zero,
            performance?.TimeType ?? string.Empty,
            performance?.Frequency,
            ModernPrDetectionStatus.Rejected,
            null,
            null,
            0,
            null,
            error);

    private static string GetInnermostMessage(Exception exception)
    {
        while (exception.InnerException is not null)
            exception = exception.InnerException;

        return exception.Message;
    }

    private sealed record ExistingPlayer(int Id, int SitePlayerId, string Name);

    private sealed record ExistingHistory(
        int PlayerId,
        int CourseId,
        string TimeType,
        int? Frequency,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        int SequenceNumber,
        string SourceSystem);

    private sealed record HistorySummary(TimeSpan BestTime, int MaximumSequenceNumber);

    private sealed record TimeTypeValidationResult(
        TimeTypeValidationStatus Status,
        string? Reason);

    private readonly record struct SeriesKey(
        int PlayerId,
        int CourseId,
        string TimeType,
        int? Frequency);

    private readonly record struct SeriesInputKey(
        int SitePlayerId,
        string CourseName,
        string TimeType,
        int? Frequency);

    private sealed record ImportPlan(
        string CourseName,
        List<PreparedPerformance> Performances,
        MkddHistoricalModernImportResult Result);

    private sealed class PreparedPerformance
    {
        public required MkddPerformance Source { get; init; }
        public string PlayerName { get; init; } = string.Empty;
        public string CourseName { get; init; } = string.Empty;
        public string NormalizedPlayerName { get; init; } = string.Empty;
        public string TimeType { get; init; } = string.Empty;
        public int? Frequency { get; init; }
        public DateTimeOffset FirstSeenAt { get; init; }
        public string DateSource { get; init; } = "ImportDate";
        public int? ExistingPlayerId { get; init; }
        public int? ExistingAliasId { get; set; }
        public string ExternalKey { get; init; } = string.Empty;
        public ModernPrDetectionStatus Status { get; set; }
        public TimeSpan? PreviousTime { get; set; }
        public TimeSpan? Improvement { get; set; }
        public int SequenceNumber { get; set; }
        public string? Error { get; set; }

        public static PreparedPerformance Rejected(
            MkddPerformance source,
            string courseName,
            string timeType,
            int? frequency,
            string error) =>
            new()
            {
                Source = source,
                PlayerName = source.PlayerName,
                CourseName = courseName,
                TimeType = timeType,
                Frequency = frequency,
                Status = ModernPrDetectionStatus.Rejected,
                Error = error
            };
    }
}
