using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.SnapshotService;
using System.Text.Encodings.Web;
using System.Text.Json;
using MKDDTracker.Scraper.Services.HistoricalImport;

if (args.Contains(
    "--analyze-historical-import",
    StringComparer.OrdinalIgnoreCase))
{
    await AnalyzeHistoricalImportAsync();
    return;
}

if (args.Contains(
    "--dry-run-historical-import",
    StringComparer.OrdinalIgnoreCase))
{
    await DryRunHistoricalImportAsync();
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