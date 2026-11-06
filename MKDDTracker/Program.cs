using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.SnapshotService;

const string filePath = "Samples/luigi-circuit.html";
const string courseName = "Luigi Circuit";

const string playerName = "Mattilde F";

var evolutionStartDate = new DateOnly(2026, 9, 1);
var evolutionEndDate = new DateOnly(2026, 9, 22);

var previousDate = new DateOnly(2026, 9, 15);
var currentDate = new DateOnly(2026, 9, 22);


// ========================================
// VERIFICATION DU FICHIER
// ========================================

if (!File.Exists(filePath))
{
    Console.WriteLine(
        $"Fichier introuvable : {filePath}");

    return;
}


// ========================================
// PARSING DU CLASSEMENT ORIGINAL
// ========================================

var html = await File.ReadAllTextAsync(filePath);

var parser = new MkddCoursePageParser();

var performances = parser.Parse(html);


// ========================================
// TOP 10 ORIGINAL
// ========================================

var originalTop10 = performances
    .OrderBy(x => x.Rank)
    .Take(10);

Console.WriteLine();
Console.WriteLine("TOP 10 ORIGINAL");
Console.WriteLine("----------------");

foreach (var performance in originalTop10)
{
    Console.WriteLine(
        $"#{performance.Rank} - " +
        $"{performance.PlayerName} - " +
        $"{FormatRaceTime(performance.Time)}");
}

Console.WriteLine();

Console.WriteLine(
    $"Performances trouvées : {performances.Count}");


// ========================================
// DATABASE
// ========================================

var options = new DbContextOptionsBuilder<MkddDbContext>()
    .UseSqlite("Data Source=mkddtracker.db")
    .Options;

using var db = new MkddDbContext(options);

await db.Database.EnsureCreatedAsync();


// ========================================
// SNAPSHOT REEL
// ========================================

var snapshotService =
    new MkddSnapshotService(db);

var rankingDate =
    DateOnly.FromDateTime(DateTime.UtcNow);

var capturedAt =
    DateTime.UtcNow;

await snapshotService.SaveSnapshotAsync(
    courseName,
    performances,
    rankingDate,
    capturedAt);

Console.WriteLine(
    "Snapshot enregistré.");


// ========================================
// VERIFICATION ENZO
// ========================================

var enzo =
    await db.Players
        .SingleOrDefaultAsync(
            x => x.Name == "Enzo Vusur");

Console.WriteLine(
    enzo is null
        ? "Enzo introuvable"
        : $"Enzo trouvé : ID {enzo.Id}");


// ========================================
// DONNEES DE TEST
// ========================================

var testSeeder =
    new MkddTestDataSeeder(db);

await testSeeder.SeedAsync(
    courseName,
    playerName,
    "Enzo Vusur",
    performances);

Console.WriteLine(
    "Données de test créées.");

// ========================================
// SERVICE DE COMPARAISON
// ========================================

var comparisonService =
    new MkddSnapshotComparisonService(db);


// ========================================
// EVOLUTION COMPLETE SUR LA PERIODE
// ========================================

var playerEvolutionService =
    new MkddPlayerEvolutionService(
        db,
        comparisonService);

var completeEvolution =
    await playerEvolutionService.AnalyzeAsync(
        playerName,
        courseName,
        evolutionStartDate,
        evolutionEndDate);

if (completeEvolution is null)
{
    Console.WriteLine();

    Console.WriteLine(
        "Impossible d'analyser l'evolution.");

    return;
}


// ========================================
// CONSTRUCTION DU RAPPORT
// ========================================

var reportService =
    new MkddEvolutionReportService();

var report =
    reportService.BuildReport(
        completeEvolution);


// ========================================
// AFFICHAGE DU RAPPORT
// ========================================

var renderer =
    new MkddEvolutionConsoleRenderer();

renderer.Render(report);

// ========================================
// FORMATAGE
// ========================================

static string FormatRaceTime(TimeSpan time)
{
    return
        $"{(int)time.TotalMinutes}:" +
        $"{time.Seconds:00}." +
        $"{time.Milliseconds:000}";
}
