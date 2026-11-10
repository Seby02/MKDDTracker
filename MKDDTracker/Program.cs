using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.SnapshotService;

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
