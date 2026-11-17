using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.SnapshotService;
using System.Text.Encodings.Web;
using System.Text.Json;
using MKDDTracker.Scraper.Services.HistoricalImport;
using System.Globalization;

if (args.Contains(
    "--dry-run-fix-pre2003-dates",
    StringComparer.OrdinalIgnoreCase))
{
    await RunPre2003DateCorrectionAsync(dryRun: true);
    return;
}

if (args.Contains(
    "--fix-pre2003-dates",
    StringComparer.OrdinalIgnoreCase))
{
    await RunPre2003DateCorrectionAsync(dryRun: false);
    return;
}

if (args.Contains(
    "--diagnose-history-years",
    StringComparer.OrdinalIgnoreCase))
{
    await DiagnoseHistoryYearsAsync();
    return;
}

int diagnosticIndex = Array.FindIndex(
    args,
    argument => string.Equals(
        argument,
        "--diagnose-modern-history",
        StringComparison.OrdinalIgnoreCase));

if (diagnosticIndex >= 0)
{
    string diagnosticPlayerName = string.Join(
        " ",
        args.Skip(diagnosticIndex + 1));
    await DiagnoseModernHistoryAsync(diagnosticPlayerName);
    return;
}

if (args.Contains(
    "--dry-run-snapshot-history-migration",
    StringComparer.OrdinalIgnoreCase))
{
    await RunSnapshotHistoryMigrationAsync(dryRun: true);
    return;
}

if (args.Contains(
    "--migrate-snapshot-history",
    StringComparer.OrdinalIgnoreCase))
{
    await RunSnapshotHistoryMigrationAsync(dryRun: false);
    return;
}

if (args.Contains(
    "--analyze-historical-import",
    StringComparer.OrdinalIgnoreCase))
{
    await AnalyzeHistoricalImportAsync();
    return;
}

static async Task RunSnapshotHistoryMigrationAsync(bool dryRun)
{
    string databasePath = MkddDatabasePaths.GetRealDatabasePath();
    if (!File.Exists(databasePath))
    {
        Console.WriteLine($"Base introuvable : {databasePath}");
        return;
    }

    var options = new DbContextOptionsBuilder<MkddDbContext>()
        .UseSqlite($"Data Source={databasePath}")
        .Options;
    await using var db = new MkddDbContext(options);
    var service = new MkddSnapshotHistoryMigrationService(db);

    if (dryRun)
    {
        Console.WriteLine("=== DRY-RUN MIGRATION SNAPSHOTS VERS HISTORIQUE ===");
        PrintSnapshotMigrationResult(await service.AnalyzeAsync());
        return;
    }

    Console.WriteLine("=== MIGRATION SNAPSHOTS VERS HISTORIQUE ===");
    Console.WriteLine("Cette commande écrit dans la base réelle.");
    Console.WriteLine("Tape MIGRER pour continuer :");
    if (!string.Equals(Console.ReadLine(), "MIGRER", StringComparison.Ordinal))
    {
        Console.WriteLine("Migration annulée.");
        return;
    }

    Action<MkddSnapshotHistoryMigrationProgress> progress = status =>
        Console.WriteLine(
            $"Progression : {status.ImportedEvents:N0}/{status.TotalEvents:N0} événements " +
            $"({status.ProcessedSeries:N0}/{status.TotalSeries:N0} séries)");
    MkddSnapshotHistoryMigrationResult result = await service.ImportAsync(progress);
    PrintSnapshotMigrationResult(result);
    Console.WriteLine($"Événements importés : {result.PrEventsImported:N0}");
}

static async Task RunPre2003DateCorrectionAsync(bool dryRun)
{
    string databasePath = MkddDatabasePaths.GetRealDatabasePath();
    if (!File.Exists(databasePath))
    {
        Console.WriteLine($"Base introuvable : {databasePath}");
        return;
    }

    var options = new DbContextOptionsBuilder<MkddDbContext>()
        .UseSqlite($"Data Source={databasePath}")
        .Options;
    await using var db = new MkddDbContext(options);
    var service = new Pre2003DateCorrectionService(db);

    if (dryRun)
    {
        Console.WriteLine("=== DRY-RUN CORRECTION DES DATES PRÉ-2003 ===");
        PrintPre2003DateCorrectionPlan(await service.AnalyzeAsync());
        return;
    }

    Console.WriteLine("=== CORRECTION DES DATES PRÉ-2003 ===");
    Console.WriteLine("Cette commande modifie la base réelle dans une transaction.");
    PrintPre2003DateCorrectionPlan(await service.AnalyzeAsync());
    Console.WriteLine("Tapez exactement CORRIGER pour continuer :");
    if (!string.Equals(Console.ReadLine(), "CORRIGER", StringComparison.Ordinal))
    {
        Console.WriteLine("Correction annulée.");
        return;
    }

    try
    {
        Pre2003DateCorrectionPlan result = await service.CorrectAsync();
        Console.WriteLine("Correction validée.");
        PrintPre2003DateCorrectionPlan(result);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Correction annulée ; transaction annulée : {exception.Message}");
        Environment.ExitCode = 1;
    }
}

static void PrintPre2003DateCorrectionPlan(Pre2003DateCorrectionPlan plan)
{
    Console.WriteLine($"Performances concernées : {plan.PerformanceCount:N0}");
    Console.WriteLine($"Messages concernés : {plan.Messages.Count:N0}");
    Console.WriteLine($"Sources concernées : {plan.SourceCount:N0}");
    foreach (Pre2003DateCorrectionMessagePlan message in plan.Messages)
    {
        string previousDates = message.PreviousFirstSeenAt.Count == 0
            ? "aucune"
            : string.Join(", ", message.PreviousFirstSeenAt.Select(date =>
                date.ToString("O", CultureInfo.InvariantCulture)));
        Console.WriteLine(
            $"MessageId {message.MessageId} : {previousDates} → " +
            message.CorrectedFirstSeenAt.ToString("O", CultureInfo.InvariantCulture) +
            $" ({message.PerformanceCount:N0} performances, {message.SourceCount:N0} sources)");
    }
}

static void PrintSnapshotMigrationResult(MkddSnapshotHistoryMigrationResult result)
{
    Console.WriteLine();
    Console.WriteLine($"Performances snapshot analysées : {result.SnapshotPerformancesAnalyzed:N0}");
    Console.WriteLine($"Première RecordDate : {FormatMigrationDate(result.EarliestRecordDate)}");
    Console.WriteLine($"Dernière RecordDate : {FormatMigrationDate(result.LatestRecordDate)}");
    Console.WriteLine($"Première RankingDate : {FormatMigrationDate(result.EarliestRankingDate)}");
    Console.WriteLine($"Dernière RankingDate : {FormatMigrationDate(result.LatestRankingDate)}");
    Console.WriteLine($"Séries joueur/circuit analysées : {result.PlayerCourseSeriesAnalyzed:N0}");
    Console.WriteLine($"Alias modernes existants : {result.ExistingModernAliases:N0}");
    Console.WriteLine($"Alias modernes à créer : {result.ModernAliasesToCreate:N0}");
    Console.WriteLine($"Événements PR existants : {result.ExistingPrEvents:N0}");
    Console.WriteLine($"Événements PR à créer : {result.PrEventsToCreate:N0}");
    Console.WriteLine($"Snapshots identiques ignorés : {result.IdenticalSnapshotsIgnored:N0}");
    Console.WriteLine($"Chronos moins bons ignorés : {result.SlowerTimesIgnored:N0}");
}

static string FormatMigrationDate(DateOnly? date) =>
    date?.ToString("dd/MM/yyyy") ?? "aucune";

static async Task DiagnoseModernHistoryAsync(string requestedPlayerName)
{
    if (string.IsNullOrWhiteSpace(requestedPlayerName))
    {
        Console.WriteLine("Usage : --diagnose-modern-history \"Nom du joueur\"");
        return;
    }

    string databasePath = MkddDatabasePaths.GetRealDatabasePath();
    if (!File.Exists(databasePath))
    {
        Console.WriteLine($"Base introuvable : {databasePath}");
        return;
    }

    var options = new DbContextOptionsBuilder<MkddDbContext>()
        .UseSqlite($"Data Source={databasePath}")
        .Options;
    await using var db = new MkddDbContext(options);

    var players = await db.Players
        .AsNoTracking()
        .Select(player => new { player.Id, player.Name })
        .ToListAsync();
    var player = players.FirstOrDefault(candidate =>
        string.Equals(
            candidate.Name,
            requestedPlayerName,
            StringComparison.OrdinalIgnoreCase));

    if (player is null)
    {
        Console.WriteLine($"Joueur moderne introuvable : {requestedPlayerName}");
        return;
    }

    var snapshotPerformances = await db.Performances
        .AsNoTracking()
        .Where(performance => performance.PlayerId == player.Id)
        .Select(performance => new
        {
            Course = performance.Snapshot.Course.Name,
            performance.Time,
            performance.RecordDate,
            RankingDate = performance.Snapshot.RankingDate
        })
        .OrderBy(performance => performance.Course)
        .ThenBy(performance => performance.RankingDate)
        .ToListAsync();

    var historicalPerformances = await db.HistoricalPerformances
        .AsNoTracking()
        .Where(performance =>
            performance.HistoricalPlayerAlias.PlayerId == player.Id)
        .Select(performance => new
        {
            Course = performance.Course.Name,
            performance.Time,
            performance.FirstSeenAt,
            performance.TimeType,
            performance.Frequency,
            performance.SourceSystem,
            performance.ExternalKey
        })
        .ToListAsync();

    historicalPerformances = historicalPerformances
        .OrderBy(performance => performance.Course)
        .ThenBy(performance => performance.FirstSeenAt)
        .ToList();

    var modernHistory = historicalPerformances
        .Where(performance => string.Equals(
            performance.SourceSystem,
            "MKDDTracker",
            StringComparison.OrdinalIgnoreCase))
        .ToList();

    Console.WriteLine($"=== DIAGNOSTIC HISTORIQUE MODERNE : {player.Name} ===");
    Console.WriteLine("Mode lecture seule : aucune donnée n'est modifiée.");
    Console.WriteLine();
    Console.WriteLine("Performances source Snapshots/Performances");
    Console.WriteLine("Circuit | Chrono | RecordDate | RankingDate");
    foreach (var performance in snapshotPerformances)
    {
        Console.WriteLine(
            $"{performance.Course} | {FormatDiagnosticTime(performance.Time)} | " +
            $"{performance.RecordDate:dd/MM/yyyy} | {performance.RankingDate:dd/MM/yyyy}");
    }

    Console.WriteLine();
    Console.WriteLine("HistoricalPerformances correspondantes");
    Console.WriteLine("Circuit | Chrono | FirstSeenAt | TimeType | Frequency | SourceSystem | ExternalKey");
    foreach (var performance in historicalPerformances)
    {
        Console.WriteLine(
            $"{performance.Course} | {FormatDiagnosticTime(performance.Time)} | " +
            $"{performance.FirstSeenAt.ToString("O", CultureInfo.InvariantCulture)} | " +
            $"{performance.TimeType} | {performance.Frequency?.ToString(CultureInfo.InvariantCulture) ?? "null"} | " +
            $"{performance.SourceSystem} | {performance.ExternalKey}");
    }

    DateTimeOffset? earliestFirstSeenAt = historicalPerformances.Count == 0
        ? null
        : historicalPerformances.Min(performance => performance.FirstSeenAt);
    DateTimeOffset? latestFirstSeenAt = historicalPerformances.Count == 0
        ? null
        : historicalPerformances.Max(performance => performance.FirstSeenAt);
    DateOnly? earliestRecordDate = snapshotPerformances.Count == 0
        ? null
        : snapshotPerformances.Min(performance => performance.RecordDate);
    DateOnly? latestRecordDate = snapshotPerformances.Count == 0
        ? null
        : snapshotPerformances.Max(performance => performance.RecordDate);

    Console.WriteLine();
    Console.WriteLine($"RecordDate min/max : {FormatDiagnosticDate(earliestRecordDate)} / {FormatDiagnosticDate(latestRecordDate)}");
    Console.WriteLine($"FirstSeenAt min/max : {FormatDiagnosticDateTime(earliestFirstSeenAt)} / {FormatDiagnosticDateTime(latestFirstSeenAt)}");
    Console.WriteLine($"Nombre de performances snapshot : {snapshotPerformances.Count:N0}");
    Console.WriteLine($"Nombre d'événements historiques SourceSystem = MKDDTracker : {modernHistory.Count:N0}");
}

static string FormatDiagnosticTime(TimeSpan time) =>
    time.ToString(@"m\:ss\.fff", CultureInfo.InvariantCulture);

static string FormatDiagnosticDate(DateOnly? date) =>
    date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "aucune";

static string FormatDiagnosticDateTime(DateTimeOffset? date) =>
    date?.ToString("O", CultureInfo.InvariantCulture) ?? "aucune";

static async Task DiagnoseHistoryYearsAsync()
{
    string databasePath = MkddDatabasePaths.GetRealDatabasePath();
    if (!File.Exists(databasePath))
    {
        Console.WriteLine($"Base introuvable : {databasePath}");
        return;
    }

    var options = new DbContextOptionsBuilder<MkddDbContext>()
        .UseSqlite($"Data Source={databasePath}")
        .Options;
    await using var db = new MkddDbContext(options);

    var performances = await db.HistoricalPerformances
        .AsNoTracking()
        .Select(performance => new
        {
            performance.Id,
            Player = performance.HistoricalPlayerAlias.Player != null
                ? performance.HistoricalPlayerAlias.Player.Name
                : performance.HistoricalPlayerAlias.ExternalName,
            Course = performance.Course.Name,
            performance.TimeType,
            performance.Frequency,
            performance.Time,
            performance.FirstSeenAt,
            performance.SourceSystem,
            performance.DateSource,
            performance.ImportStatus,
            performance.ExternalKey
        })
        .ToListAsync();

    DateTimeOffset? earliest = performances.Count == 0
        ? null
        : performances.Min(performance => performance.FirstSeenAt);
    DateTimeOffset? latest = performances.Count == 0
        ? null
        : performances.Max(performance => performance.FirstSeenAt);

    static int GetUtcYear(DateTimeOffset date) =>
        date.ToUniversalTime().UtcDateTime.Year;

    Console.WriteLine("=== DIAGNOSTIC DES ANNÉES HISTORIQUES ===");
    Console.WriteLine("Mode lecture seule : aucune donnée n'est modifiée.");
    Console.WriteLine($"FirstSeenAt minimum : {FormatDiagnosticDateTime(earliest)}");
    Console.WriteLine($"FirstSeenAt maximum : {FormatDiagnosticDateTime(latest)}");
    Console.WriteLine();

    var before2003 = performances
        .Where(performance => GetUtcYear(performance.FirstSeenAt) < 2003)
        .OrderBy(performance => performance.FirstSeenAt.ToUniversalTime())
        .ThenBy(performance => performance.Id)
        .ToList();
    var yahooPerformanceIds = before2003
        .Where(performance => string.Equals(
            performance.SourceSystem,
            "YahooGroups",
            StringComparison.OrdinalIgnoreCase))
        .Select(performance => performance.Id)
        .ToList();
    var yahooSources = await db.HistoricalPerformanceSources
        .AsNoTracking()
        .Where(source => yahooPerformanceIds.Contains(source.HistoricalPerformanceId))
        .Select(source => new
        {
            source.HistoricalPerformanceId,
            source.MessageId,
            source.PublishedAt,
            source.SourceFile,
            source.OriginalTime,
            source.SourceLine,
            source.ImportStatus
        })
        .ToListAsync();
    var sourcesByPerformance = yahooSources
        .GroupBy(source => source.HistoricalPerformanceId)
        .ToDictionary(group => group.Key, group => group.ToList());

    Console.WriteLine("Performances antérieures au 01/01/2003 (FirstSeenAt UTC)");
    Console.WriteLine("Joueur | Circuit | Type | Fréquence | Chrono | FirstSeenAt | SourceSystem | DateSource | ImportStatus | ExternalKey");
    foreach (var performance in before2003)
    {
        Console.WriteLine(
            $"{performance.Player} | {performance.Course} | {performance.TimeType} | " +
            $"{performance.Frequency?.ToString(CultureInfo.InvariantCulture) ?? "Inconnue"} | " +
            $"{FormatDiagnosticTime(performance.Time)} | " +
            $"{performance.FirstSeenAt.ToString("O", CultureInfo.InvariantCulture)} | " +
            $"{performance.SourceSystem} | {performance.DateSource} | {performance.ImportStatus} | {performance.ExternalKey ?? "aucune"}");

        if (string.Equals(performance.SourceSystem, "YahooGroups", StringComparison.OrdinalIgnoreCase))
        {
            if (sourcesByPerformance.TryGetValue(performance.Id, out var sources) && sources.Count > 0)
            {
                foreach (var source in sources)
                {
                    Console.WriteLine(
                        $"  Source Yahoo | MessageId: {source.MessageId} | " +
                        $"PublishedAt: {FormatDiagnosticDateTime(source.PublishedAt)} | " +
                        $"SourceFile: {source.SourceFile ?? "aucun"} | " +
                        $"OriginalTime: {source.OriginalTime ?? "aucun"} | " +
                        $"SourceLine: {source.SourceLine ?? "aucune"} | ImportStatus: {source.ImportStatus}");
                }
            }
            else
            {
                Console.WriteLine("  Source Yahoo : aucune source associée.");
            }
        }
    }

    if (before2003.Count == 0)
        Console.WriteLine("Aucune performance antérieure au 01/01/2003.");
    Console.WriteLine();

    Console.WriteLine("Performances par année UTC");
    foreach (var yearGroup in performances
                 .GroupBy(performance => GetUtcYear(performance.FirstSeenAt))
                 .OrderBy(group => group.Key))
    {
        Console.WriteLine($"{yearGroup.Key} : {yearGroup.Count():N0}");
    }

    Console.WriteLine();
    Console.WriteLine("Performances par année et SourceSystem (2012 et suivantes, UTC)");
    foreach (var yearGroup in performances
                 .Where(performance => GetUtcYear(performance.FirstSeenAt) >= 2012)
                 .GroupBy(performance => GetUtcYear(performance.FirstSeenAt))
                 .OrderBy(group => group.Key))
    {
        Console.WriteLine($"{yearGroup.Key} :");
        foreach (var sourceGroup in yearGroup
                     .GroupBy(performance => performance.SourceSystem)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {sourceGroup.Key} : {sourceGroup.Count():N0}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("20 dernières performances de 2019 (FirstSeenAt UTC)");
    Console.WriteLine("Joueur | Circuit | Type | Fréquence | Chrono | FirstSeenAt | SourceSystem | DateSource | ImportStatus");
    var latest2019 = performances
        .Where(performance => GetUtcYear(performance.FirstSeenAt) == 2019)
        .OrderByDescending(performance => performance.FirstSeenAt.ToUniversalTime())
        .ThenByDescending(performance => performance.Id)
        .Take(20)
        .ToList();

    foreach (var performance in latest2019)
    {
        Console.WriteLine(
            $"{performance.Player} | {performance.Course} | {performance.TimeType} | " +
            $"{performance.Frequency?.ToString(CultureInfo.InvariantCulture) ?? "Inconnue"} | " +
            $"{FormatDiagnosticTime(performance.Time)} | " +
            $"{performance.FirstSeenAt.ToString("O", CultureInfo.InvariantCulture)} | " +
            $"{performance.SourceSystem} | {performance.DateSource} | {performance.ImportStatus}");
    }

    if (latest2019.Count == 0)
        Console.WriteLine("Aucune performance en 2019.");
}

if (args.Contains(
    "--dry-run-historical-import",
    StringComparer.OrdinalIgnoreCase))
{
    await DryRunHistoricalImportAsync();
    return;
}

if (args.Contains(
    "--import-historical",
    StringComparer.OrdinalIgnoreCase))
{
    await ImportHistoricalAsync();
    return;
}


const string seedDemoArgument = "--seed-demo";
const string resetDemoArgument = "--reset-demo";
const string courseName = "Luigi Circuit";
const string playerName = "Mattilde F";
const string opponentName = "Enzo Vusur";

if (!args.Contains(seedDemoArgument, StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("MKDDTracker n'ajoute aucune donnée de démonstration au démarrage normal.");
    Console.WriteLine();
    Console.WriteLine("Pour créer ou actualiser la base de démonstration séparée :");
    Console.WriteLine("  dotnet run --project MKDDTracker -- --seed-demo");
    Console.WriteLine();
    Console.WriteLine("Pour la recréer entièrement :");
    Console.WriteLine("  dotnet run --project MKDDTracker -- --seed-demo --reset-demo");
    return;
}

var samplePath = Path.Combine(
    AppContext.BaseDirectory,
    "Samples",
    "luigi-circuit.html");

if (!File.Exists(samplePath))
{
    Console.Error.WriteLine($"Fichier de démonstration introuvable : {samplePath}");
    Environment.ExitCode = 1;
    return;
}

MkddDatabasePaths.EnsureApplicationDataDirectoryExists();
var demoDatabasePath = MkddDatabasePaths.DemoDatabasePath;

if (args.Contains(resetDemoArgument, StringComparer.OrdinalIgnoreCase))
{
    MkddDatabaseFiles.Delete(demoDatabasePath);
}

var options = new DbContextOptionsBuilder<MkddDbContext>()
    .UseSqlite($"Data Source={demoDatabasePath}")
    .Options;

await using var db = new MkddDbContext(options);
var databaseInitializer = new MkddDatabaseInitializer(
    new MkddDatabaseBackupService());
await databaseInitializer.InitializeAsync(
    db,
    demoDatabasePath,
    DatabaseKind.Demo);

var html = await File.ReadAllTextAsync(samplePath);
var performances = new MkddCoursePageParser().Parse(html);

var demoSeeder = new MkddDemoDataSeeder(db);
await demoSeeder.SeedAsync(
    courseName,
    playerName,
    opponentName,
    performances);

Console.WriteLine($"Base de démonstration prête : {demoDatabasePath}");
Console.WriteLine("La base réelle mkddtracker.db n'a pas été modifiée.");

var comparisonService = new MkddSnapshotComparisonService(db);
var playerEvolutionService = new MkddPlayerEvolutionService(
    db,
    comparisonService);

var evolution = await playerEvolutionService.AnalyzeAsync(
    playerName,
    courseName,
    new DateOnly(2026, 9, 1),
    new DateOnly(2026, 9, 22));

if (evolution is null)
{
    Console.Error.WriteLine("Impossible d'analyser le scénario de démonstration.");
    Environment.ExitCode = 1;
    return;
}

var report = new MkddEvolutionReportService().BuildReport(evolution);
new MkddEvolutionConsoleRenderer().Render(report);



static async Task DryRunHistoricalImportAsync()
{
    Console.WriteLine(
        "=== DRY-RUN IMPORT HISTORIQUE YAHOO ===");

    Console.WriteLine();

    string projectDirectory =
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                ".."));

    string importFile =
        Path.Combine(
            projectDirectory,
            "imports",
            "mkddtracker-import-approved.json");

    if (!File.Exists(importFile))
    {
        Console.WriteLine(
            $"Fichier introuvable : {importFile}");

        return;
    }

    string databasePath =
        MkddDatabasePaths.RealDatabasePath;

    if (!File.Exists(databasePath))
    {
        Console.WriteLine(
            $"Base introuvable : {databasePath}");

        return;
    }

    Console.WriteLine(
        $"Import : {importFile}");

    Console.WriteLine(
        $"Base   : {databasePath}");

    Console.WriteLine();

    var dbOptions =
        new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite(
                $"Data Source={databasePath}")
            .Options;

    await using var db =
        new MkddDbContext(
            dbOptions);

    var service =
        new MkddHistoricalImportDryRunService(
            db);

    HistoricalImportDryRunResult result =
        await service.AnalyzeAsync(
            importFile);

    Console.WriteLine("=== RÉSULTAT ===");
    Console.WriteLine();

    Console.WriteLine(
        $"Entrées JSON              : " +
        $"{result.InputRecordsCount:N0}");

    Console.WriteLine(
        $"Enregistrements invalides : " +
        $"{result.InvalidRecordsCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Schéma historique présent : " +
        $"{result.HistoricalSchemaExists}");

    Console.WriteLine();

    Console.WriteLine(
        $"Circuits déjà présents    : " +
        $"{result.CoursesAlreadyExistingCount:N0}");

    Console.WriteLine(
        $"Circuits à créer          : " +
        $"{result.CoursesToCreateCount:N0}");

    foreach (string courseName
             in result.CoursesToCreate)
    {
        Console.WriteLine(
            $"  - {courseName}");
    }

    Console.WriteLine();

    Console.WriteLine(
        $"Alias existants           : " +
        $"{result.AliasesAlreadyExistingCount:N0}");

    Console.WriteLine(
        $"Alias à créer             : " +
        $"{result.AliasesToCreateCount:N0}");

    Console.WriteLine(
        $"Alias avec joueur moderne : " +
        $"{result.AliasesMatchedToPlayersCount:N0}");

    Console.WriteLine(
        $"Alias sans joueur moderne : " +
        $"{result.AliasesWithoutPlayerCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Performances existantes   : " +
        $"{result.PerformancesAlreadyExistingCount:N0}");

    Console.WriteLine(
        $"Performances à créer      : " +
        $"{result.PerformancesToCreateCount:N0}");

    Console.WriteLine(
        $"Sources à créer           : " +
        $"{result.SourcesToCreateCount:N0}");

    if (result.Errors.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Erreurs");

        foreach (string error
                 in result.Errors)
        {
            Console.WriteLine(
                $"  - {error}");
        }
    }
}

static async Task AnalyzeHistoricalImportAsync()
{
    Console.WriteLine(
        "=== ANALYSE IMPORT HISTORIQUE YAHOO ===");

    Console.WriteLine();

    string projectDirectory =
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                ".."));

    string importDirectory =
        Path.Combine(
            projectDirectory,
            "imports");

    string importFile =
        Path.Combine(
            importDirectory,
            "mkddtracker-import-approved.json");

    string outputDirectory =
        Path.Combine(
            importDirectory,
            "analysis");

    if (!File.Exists(importFile))
    {
        Console.WriteLine(
            $"Fichier introuvable : {importFile}");

        return;
    }

    Directory.CreateDirectory(
        outputDirectory);

    //
    // IMPORTANT :
    // ici nous utiliserons ensuite exactement
    // la même configuration que MkddDbContextFactory.
    //

    string databasePath =
        MkddDatabasePaths.GetRealDatabasePath();

    Console.WriteLine(
        $"Import : {importFile}");

    Console.WriteLine(
        $"Base   : {databasePath}");

    Console.WriteLine();

    var dbOptions =
        new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite(
                $"Data Source={databasePath}")
            .Options;

    await using var db =
        new MkddDbContext(
            dbOptions);

    var analyzer =
        new MkddHistoricalImportAnalyzer(
            db);

    HistoricalImportAnalysisResult result =
        await analyzer.AnalyzeAsync(
            importFile);

    var jsonOptions =
        new JsonSerializerOptions
        {
            WriteIndented = true,

            Encoder =
                JavaScriptEncoder
                    .UnsafeRelaxedJsonEscaping
        };

    WriteJson(
        Path.Combine(
            outputDirectory,
            "historical-import-analysis.json"),
        result,
        jsonOptions);

    WriteJson(
        Path.Combine(
            outputDirectory,
            "historical-player-matches.json"),
        result.Players,
        jsonOptions);

    WriteJson(
        Path.Combine(
            outputDirectory,
            "historical-course-matches.json"),
        result.Courses,
        jsonOptions);

    WriteJson(
        Path.Combine(
            outputDirectory,
            "historical-blocked-records.json"),
        result.BlockedRecords,
        jsonOptions);

    Console.WriteLine(
        "=== RÉSULTAT ===");

    Console.WriteLine();

    Console.WriteLine(
        $"Entrées JSON           : " +
        $"{result.InputRecordsCount:N0}");

    Console.WriteLine(
        $"Entrées mappables      : " +
        $"{result.MappableRecordsCount:N0}");

    Console.WriteLine(
        $"Entrées bloquées       : " +
        $"{result.BlockedRecordsCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Joueurs Yahoo uniques  : " +
        $"{result.UniqueExternalPlayersCount:N0}");

    Console.WriteLine(
        $"Joueurs correspondants : " +
        $"{result.MatchedPlayersCount:N0}");

    Console.WriteLine(
        $"Joueurs non trouvés    : " +
        $"{result.UnmatchedPlayersCount:N0}");

    Console.WriteLine(
        $"Joueurs ambigus        : " +
        $"{result.AmbiguousPlayersCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Circuits déjà présents          : " +
        $"{result.MatchedCoursesCount:N0}");

    Console.WriteLine(
        $"Circuits à créer                : " +
        $"{result.CreatableCoursesCount:N0}");

    Console.WriteLine(
        $"Codes circuits non pris en charge : " +
        $"{result.UnsupportedCoursesCount:N0}");

    Console.WriteLine(
        $"Entrées avec circuit à créer    : " +
        $"{result.RecordsUsingCreatableCoursesCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Rapports : {outputDirectory}");
}

static void WriteJson<T>(
    string filePath,
    T value,
    JsonSerializerOptions options)
{
    string json =
        JsonSerializer.Serialize(
            value,
            options);

    File.WriteAllText(
        filePath,
        json);
}

static async Task ImportHistoricalAsync()
{
    Console.WriteLine(
        "=== IMPORT HISTORIQUE YAHOO ===");

    Console.WriteLine();

    string projectDirectory =
        Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                ".."));

    string importFile =
        Path.Combine(
            projectDirectory,
            "imports",
            "mkddtracker-import-approved.json");

    if (!File.Exists(importFile))
    {
        Console.WriteLine(
            $"Fichier introuvable : {importFile}");

        return;
    }

    string databasePath =
        MkddDatabasePaths.RealDatabasePath;

    if (!File.Exists(databasePath))
    {
        Console.WriteLine(
            $"Base introuvable : {databasePath}");

        return;
    }

    Console.WriteLine(
        $"Import : {importFile}");

    Console.WriteLine(
        $"Base   : {databasePath}");

    Console.WriteLine();

    Console.WriteLine(
        "ATTENTION : cette commande écrit dans la base réelle.");

    Console.WriteLine(
        "Tape IMPORTER pour continuer :");

    string? confirmation =
        Console.ReadLine();

    if (!string.Equals(
            confirmation,
            "IMPORTER",
            StringComparison.Ordinal))
    {
        Console.WriteLine(
            "Import annulé.");

        return;
    }

    var dbOptions =
        new DbContextOptionsBuilder<MkddDbContext>()
            .UseSqlite(
                $"Data Source={databasePath}")
            .Options;

    await using var db =
        new MkddDbContext(
            dbOptions);

    var service =
        new MkddHistoricalImportService(
            db);

    HistoricalImportExecutionResult result =
        await service.ImportAsync(
            importFile);

    Console.WriteLine();
    Console.WriteLine("=== RÉSULTAT ===");
    Console.WriteLine();

    Console.WriteLine(
        $"Entrées JSON              : " +
        $"{result.InputRecordsCount:N0}");

    Console.WriteLine(
        $"Enregistrements invalides : " +
        $"{result.InvalidRecordsCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Circuits créés            : " +
        $"{result.CoursesCreatedCount:N0}");

    Console.WriteLine(
        $"Alias créés               : " +
        $"{result.AliasesCreatedCount:N0}");

    Console.WriteLine(
        $"Alias avec joueur moderne : " +
        $"{result.AliasesMatchedToPlayersCount:N0}");

    Console.WriteLine(
        $"Alias sans joueur moderne : " +
        $"{result.AliasesWithoutPlayerCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Performances existantes   : " +
        $"{result.PerformancesExistingCount:N0}");

    Console.WriteLine(
        $"Performances créées       : " +
        $"{result.PerformancesCreatedCount:N0}");

    Console.WriteLine(
        $"Sources créées            : " +
        $"{result.SourcesCreatedCount:N0}");

    Console.WriteLine();

    Console.WriteLine(
        $"Transaction validée       : " +
        $"{result.TransactionCommitted}");

    if (result.Errors.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Erreurs");

        foreach (string error
                 in result.Errors)
        {
            Console.WriteLine(
                $"  - {error}");
        }
    }
}