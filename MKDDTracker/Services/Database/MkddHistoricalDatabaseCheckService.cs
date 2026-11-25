using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;

namespace MKDDTracker.Scraper.Services.Database;

public sealed class MkddHistoricalDatabaseCheckService
{
    private static readonly DateTimeOffset EarliestPlausibleFirstSeenAt =
        new(1990, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IDbContextFactory<MkddDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public MkddHistoricalDatabaseCheckService(
        IDbContextFactory<MkddDbContext> dbFactory,
        TimeProvider? timeProvider = null)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MkddHistoricalDatabaseCheckResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        List<HistoricalDatabaseCheckItem> checks = [];
        List<string> warnings = [];
        List<string> errors = [];
        List<string> pendingMigrations = [];
        bool databaseAccessible;

        try
        {
            databaseAccessible = await db.Database.CanConnectAsync(cancellationToken);
            AddCheck(
                checks,
                warnings,
                errors,
                "Base accessible",
                databaseAccessible ? DatabaseCheckState.Passed : DatabaseCheckState.Error,
                databaseAccessible ? "Connexion SQLite réussie." : "La base SQLite est inaccessible.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            databaseAccessible = false;
            AddCheck(
                checks,
                warnings,
                errors,
                "Base accessible",
                DatabaseCheckState.Error,
                $"Connexion SQLite impossible : {exception.Message}");
        }

        if (!databaseAccessible)
        {
            return CreateResult(
                checks,
                warnings,
                errors,
                pendingMigrations,
                new HistoricalDatabaseStatistics(0, 0, 0, 0, 0, null, null, 0, 0, 0, 0, 0, 0, 0, 0),
                new HistoricalDatabaseIntegrityStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                databaseAccessible);
        }

        try
        {
            pendingMigrations = (await db.Database
                    .GetPendingMigrationsAsync(cancellationToken))
                .ToList();
            AddCheck(
                checks,
                warnings,
                errors,
                "Migrations EF",
                pendingMigrations.Count == 0 ? DatabaseCheckState.Passed : DatabaseCheckState.Error,
                pendingMigrations.Count == 0
                    ? "Toutes les migrations applicables sont à jour."
                    : $"Migrations en attente : {string.Join(", ", pendingMigrations)}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            errors.Add($"Vérification des migrations impossible : {exception.Message}");
            checks.Add(new HistoricalDatabaseCheckItem(
                "Migrations EF",
                DatabaseCheckState.Error,
                exception.Message));
        }

        long? coursesTableCount = await CheckTableAsync(
            db.Courses,
            "Courses",
            checks,
            warnings,
            errors,
            cancellationToken);
        long? playersTableCount = await CheckTableAsync(
            db.Players,
            "Players",
            checks,
            warnings,
            errors,
            cancellationToken);
        long? aliasesTableCount = await CheckTableAsync(
            db.HistoricalPlayerAliases,
            "HistoricalPlayerAliases",
            checks,
            warnings,
            errors,
            cancellationToken);
        long? performancesTableCount = await CheckTableAsync(
            db.HistoricalPerformances,
            "HistoricalPerformances",
            checks,
            warnings,
            errors,
            cancellationToken);
        long? sourcesTableCount = await CheckTableAsync(
            db.HistoricalPerformanceSources,
            "HistoricalPerformanceSources",
            checks,
            warnings,
            errors,
            cancellationToken);

        List<int>? courseIds = coursesTableCount is null
            ? null
            : await ReadAsync(
                db.Courses.AsNoTracking().Select(course => course.Id),
                "Courses",
                checks,
                warnings,
                errors,
                cancellationToken);
        List<int>? playerIds = playersTableCount is null
            ? null
            : await ReadAsync(
                db.Players.AsNoTracking().Select(player => player.Id),
                "Players",
                checks,
                warnings,
                errors,
                cancellationToken);
        List<AliasDiagnostic>? aliases = aliasesTableCount is null
            ? null
            : await ReadAsync(
                db.HistoricalPlayerAliases
                    .AsNoTracking()
                    .Select(alias => new AliasDiagnostic(
                        alias.Id,
                        alias.PlayerId,
                        alias.ResolutionStatus)),
                "HistoricalPlayerAliases",
                checks,
                warnings,
                errors,
                cancellationToken);
        List<PerformanceDiagnostic>? performances = performancesTableCount is null
            ? null
            : await ReadAsync(
                db.HistoricalPerformances
                    .AsNoTracking()
                    .Select(performance => new PerformanceDiagnostic(
                        performance.Id,
                        performance.ExternalKey,
                        performance.HistoricalPlayerAliasId,
                        performance.CourseId,
                        performance.TimeType,
                        performance.Frequency,
                        performance.Time,
                        performance.FirstSeenAt,
                        performance.PreviousTime,
                        performance.Improvement,
                        performance.SourceSystem,
                        performance.ImportStatus,
                        performance.DateSource)),
                "HistoricalPerformances",
                checks,
                warnings,
                errors,
                cancellationToken);

        HistoricalDatabaseStatistics historical = CreateHistoricalStatistics(
            performances ?? [],
            aliases ?? [],
            playerIds ?? [],
            sourcesTableCount ?? 0);
        HistoricalDatabaseIntegrityStatistics integrity = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        if (performances is not null && aliases is not null && courseIds is not null && playerIds is not null)
        {
            integrity = CheckIntegrity(performances, aliases, courseIds, playerIds, checks, warnings, errors);
        }       

        return CreateResult(
            checks,
            warnings,
            errors,
            pendingMigrations,
            historical,
            integrity,
            databaseAccessible);
    }

    private static async Task<long?> CheckTableAsync<TEntity>(
        IQueryable<TEntity> query,
        string tableName,
        List<HistoricalDatabaseCheckItem> checks,
        List<string> warnings,
        List<string> errors,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        try
        {
            long count = await query.AsNoTracking().LongCountAsync(cancellationToken);
            AddCheck(
                checks,
                warnings,
                errors,
                tableName,
                DatabaseCheckState.Passed,
                $"Table accessible ({count:N0} lignes).");
            return count;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AddCheck(
                checks,
                warnings,
                errors,
                tableName,
                DatabaseCheckState.Error,
                $"Table inaccessible : {exception.Message}");
            return null;
        }
    }

    private static async Task<List<T>?> ReadAsync<T>(
        IQueryable<T> query,
        string subject,
        List<HistoricalDatabaseCheckItem> checks,
        List<string> warnings,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            return await query.ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AddCheck(
                checks,
                warnings,
                errors,
                $"Lecture {subject}",
                DatabaseCheckState.Error,
                $"Lecture impossible : {exception.Message}");
            return null;
        }
    }

    private HistoricalDatabaseIntegrityStatistics CheckIntegrity(
        IReadOnlyList<PerformanceDiagnostic> performances,
        IReadOnlyList<AliasDiagnostic> aliases,
        IReadOnlyList<int> courseIds,
        IReadOnlyList<int> playerIds,
        List<HistoricalDatabaseCheckItem> checks,
        List<string> warnings,
        List<string> errors)
    {
        HashSet<int> aliasIds = aliases.Select(alias => alias.Id).ToHashSet();
        HashSet<int> courseIdSet = courseIds.ToHashSet();
        HashSet<int> playerIdSet = playerIds.ToHashSet();
        DateTimeOffset latestPlausibleDate = _timeProvider.GetUtcNow().AddDays(1);
        long performancesWithoutAlias = performances.LongCount(performance =>
            !aliasIds.Contains(performance.HistoricalPlayerAliasId));
        long performancesWithoutCourse = performances.LongCount(performance =>
            !courseIdSet.Contains(performance.CourseId));
        long emptyExternalKeys = performances.LongCount(performance =>
            string.IsNullOrWhiteSpace(performance.ExternalKey));
        long duplicateExternalKeyGroups = performances
            .Where(performance => !string.IsNullOrWhiteSpace(performance.ExternalKey))
            .GroupBy(performance => performance.ExternalKey, StringComparer.Ordinal)
            .LongCount(group => group.Count() > 1);
        long nonPositiveTimes = performances.LongCount(performance => performance.Time <= TimeSpan.Zero);
        long invalidTimeTypes = performances.LongCount(performance =>
            performance.TimeType is not "ThreeLap" and not "Flap");
        long invalidFrequencies = performances.LongCount(performance =>
            performance.Frequency is not null and not 50 and not 60);
        long implausibleFirstSeenDates = performances.LongCount(performance =>
            performance.FirstSeenAt < EarliestPlausibleFirstSeenAt ||
            performance.FirstSeenAt > latestPlausibleDate);
        long negativeApprovedImprovements = performances.LongCount(performance =>
            IsApproved(performance) && performance.Improvement < TimeSpan.Zero);
        long incoherentApprovedPrMetadata = performances.LongCount(IsIncoherentApprovedPr);
        long matchedAliasesWithoutPlayer = aliases.LongCount(alias =>
            string.Equals(alias.ResolutionStatus, "Matched", StringComparison.OrdinalIgnoreCase) &&
            alias.PlayerId is null);
        long aliasesPointingToMissingPlayers = aliases.LongCount(alias =>
            alias.PlayerId is int playerId && !playerIdSet.Contains(playerId));
        long unresolvedAliasesWithoutPlayer = aliases.LongCount(alias =>
            alias.PlayerId is null &&
            !string.Equals(alias.ResolutionStatus, "Matched", StringComparison.OrdinalIgnoreCase));

        AddIntegrityCheck(checks, errors, "HistoricalPerformance sans alias valide", performancesWithoutAlias);
        AddIntegrityCheck(checks, errors, "Performance sans circuit valide", performancesWithoutCourse);
        AddIntegrityCheck(checks, errors, "ExternalKey vide", emptyExternalKeys);
        AddIntegrityCheck(checks, errors, "Groupes d'ExternalKey en doublon", duplicateExternalKeyGroups);
        AddIntegrityCheck(checks, errors, "Time <= 0", nonPositiveTimes);
        AddIntegrityCheck(checks, errors, "TimeType hors ThreeLap/Flap", invalidTimeTypes);
        AddIntegrityCheck(checks, errors, "Fréquence hors 50/60/null", invalidFrequencies);
        AddIntegrityCheck(checks, errors, "FirstSeenAt manifestement non plausible", implausibleFirstSeenDates);
        AddIntegrityCheck(checks, errors, "Improvement négatif sur PR approuvé", negativeApprovedImprovements);
        AddIntegrityCheck(checks, errors, "PreviousTime/Improvement incohérents sur PR approuvé", incoherentApprovedPrMetadata);
        AddIntegrityCheck(checks, errors, "Alias Matched sans PlayerId", matchedAliasesWithoutPlayer);
        AddIntegrityCheck(checks, errors, "Alias lié à un joueur absent", aliasesPointingToMissingPlayers);

        if (unresolvedAliasesWithoutPlayer > 0)
        {
            string warning = $"{unresolvedAliasesWithoutPlayer:N0} alias non résolus n'ont pas encore de joueur moderne lié.";
            warnings.Add(warning);
            checks.Add(new HistoricalDatabaseCheckItem(
                "Alias non résolus sans joueur",
                DatabaseCheckState.Warning,
                warning));
        }
        else
        {
            checks.Add(new HistoricalDatabaseCheckItem(
                "Alias non résolus sans joueur",
                DatabaseCheckState.Passed,
                "Aucun alias non résolu sans joueur.") );
        }

        return new HistoricalDatabaseIntegrityStatistics(
            performancesWithoutAlias,
            performancesWithoutCourse,
            emptyExternalKeys,
            duplicateExternalKeyGroups,
            nonPositiveTimes,
            invalidTimeTypes,
            invalidFrequencies,
            implausibleFirstSeenDates,
            negativeApprovedImprovements,
            incoherentApprovedPrMetadata,
            matchedAliasesWithoutPlayer,
            aliasesPointingToMissingPlayers,
            unresolvedAliasesWithoutPlayer);
    }

    private static bool IsIncoherentApprovedPr(PerformanceDiagnostic performance)
    {
        if (!IsApproved(performance))
            return false;

        if (performance.PreviousTime is null)
            return performance.Improvement is not null;

        TimeSpan expectedImprovement = performance.PreviousTime.Value - performance.Time;
        return expectedImprovement <= TimeSpan.Zero || performance.Improvement != expectedImprovement;
    }

    private static bool IsApproved(PerformanceDiagnostic performance) =>
        string.Equals(performance.ImportStatus, "Approved", StringComparison.OrdinalIgnoreCase);

    private static HistoricalDatabaseStatistics CreateHistoricalStatistics(
        IReadOnlyList<PerformanceDiagnostic> performances,
        IReadOnlyList<AliasDiagnostic> aliases,
        IReadOnlyList<int> playerIds,
        long sourceCount)
    {
        HashSet<int> playerIdSet = playerIds.ToHashSet();
        DateTimeOffset[] firstSeenDates = performances
            .Select(performance => performance.FirstSeenAt)
            .ToArray();

        return new HistoricalDatabaseStatistics(
            performances.Select(performance => performance.CourseId).Distinct().LongCount(),
            aliases.Count,
            aliases
                .Where(alias => alias.PlayerId is int playerId && playerIdSet.Contains(playerId))
                .Select(alias => alias.PlayerId)
                .Distinct()
                .LongCount(),
            performances.Count,
            sourceCount,
            firstSeenDates.Length == 0 ? null : firstSeenDates.Min(),
            firstSeenDates.Length == 0 ? null : firstSeenDates.Max(),
            performances.LongCount(performance => performance.SourceSystem == "YahooGroups"),
            performances.LongCount(performance => performance.SourceSystem == "MKDDTracker"),
            performances.LongCount(performance => performance.TimeType == "ThreeLap"),
            performances.LongCount(performance => performance.TimeType == "Flap"),
            performances.LongCount(performance => performance.TimeType == "Unknown"),
            performances.LongCount(performance => performance.Frequency == 50),
            performances.LongCount(performance => performance.Frequency == 60),
            performances.LongCount(performance => performance.Frequency is null));
    }   

    private static void AddIntegrityCheck(
        List<HistoricalDatabaseCheckItem> checks,
        List<string> errors,
        string name,
        long count)
    {
        if (count == 0)
        {
            checks.Add(new HistoricalDatabaseCheckItem(name, DatabaseCheckState.Passed, "Aucune anomalie."));
            return;
        }

        string message = $"{count:N0} anomalie(s) détectée(s).";
        errors.Add($"{name} : {message}");
        checks.Add(new HistoricalDatabaseCheckItem(name, DatabaseCheckState.Error, message));
    }

    private static void AddCheck(
        List<HistoricalDatabaseCheckItem> checks,
        List<string> warnings,
        List<string> errors,
        string name,
        DatabaseCheckState state,
        string details)
    {
        checks.Add(new HistoricalDatabaseCheckItem(name, state, details));
        if (state == DatabaseCheckState.Warning)
            warnings.Add(details);
        else if (state == DatabaseCheckState.Error)
            errors.Add(details);
    }

    private static MkddHistoricalDatabaseCheckResult CreateResult(
        IReadOnlyList<HistoricalDatabaseCheckItem> checks,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> pendingMigrations,
        HistoricalDatabaseStatistics historical,
        HistoricalDatabaseIntegrityStatistics integrity,
        bool databaseAccessible)
    {
        HistoricalDatabaseHealthStatus status = errors.Count > 0
            ? HistoricalDatabaseHealthStatus.Error
            : warnings.Count > 0
                ? HistoricalDatabaseHealthStatus.Warning
                : HistoricalDatabaseHealthStatus.Healthy;

        return new MkddHistoricalDatabaseCheckResult(
            status,
            databaseAccessible,
            checks,
            warnings,
            errors,
            pendingMigrations,
            historical,
            integrity);
    }

    private sealed record AliasDiagnostic(int Id, int? PlayerId, string ResolutionStatus);

    private sealed record PerformanceDiagnostic(
        long Id,
        string ExternalKey,
        int HistoricalPlayerAliasId,
        int CourseId,
        string TimeType,
        int? Frequency,
        TimeSpan Time,
        DateTimeOffset FirstSeenAt,
        TimeSpan? PreviousTime,
        TimeSpan? Improvement,
        string SourceSystem,
        string ImportStatus,
        string DateSource);
}
