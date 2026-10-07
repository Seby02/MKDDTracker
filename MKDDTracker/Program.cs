using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services;
using MKDDTracker.Scraper.Services.EvolutionService;
using MKDDTracker.Scraper.Services.SnapshotService;

const string filePath = "Samples/luigi-circuit.html";
const string courseName = "Luigi Circuit";

if (!File.Exists(filePath))
{
    Console.WriteLine($"Fichier introuvable : {filePath}");
    return;
}

var html = await File.ReadAllTextAsync(filePath);

var parser = new MkddCoursePageParser();

var performances = parser.Parse(html);

var originalTop10 = performances
    .OrderBy(x => x.Rank)
    .Take(10);

Console.WriteLine();
Console.WriteLine("TOP 10 ORIGINAL");

foreach (var performance in originalTop10)
{
    Console.WriteLine(
        $"#{performance.Rank} - " +
        $"{performance.PlayerName} - " +
        $"{FormatRaceTime(performance.Time)}");
}

Console.WriteLine(
    $"Performances trouvées : {performances.Count}");

await using var db = new MkddDbContext();

await db.Database.EnsureCreatedAsync();

var snapshotService = new MkddSnapshotService(db);

var rankingDate = DateOnly.FromDateTime(DateTime.UtcNow);
var capturedAt = DateTime.UtcNow;

await snapshotService.SaveSnapshotAsync(
    courseName,
    performances,
    rankingDate,
    capturedAt);

Console.WriteLine("Snapshot enregistré.");

var enzo = await db.Players
    .SingleOrDefaultAsync(x => x.Name == "Enzo Vusur");

Console.WriteLine(
    enzo is null
        ? "Enzo introuvable"
        : $"Enzo trouvé : ID {enzo.Id}");

var testSeeder = new MkddTestDataSeeder(db);

await testSeeder.SeedAsync(
    courseName,
    "Mattilde F",
    "Enzo Vusur",
    performances);

Console.WriteLine("Données de test créées.");

var evolutionService = new MkddEvolutionService(db);

var evolution =
    await evolutionService.GetPlayerEvolutionAsync(
        "Mattilde F",
        courseName,
        new DateOnly(2026, 9, 1),
        new DateOnly(2026, 9, 22));

if (evolution is null)
{
    Console.WriteLine("Joueur introuvable.");
    return;
}

Console.WriteLine();
Console.WriteLine(
    $"{evolution.PlayerName} — {evolution.CourseName}");

Console.WriteLine(
    "------------------------------------------------");

foreach (var point in evolution.Points)
{
    var rankChange = point.RankChange switch
    {
        null => "-",
        > 0 => $"+{point.RankChange}",
        < 0 => $"-{Math.Abs(point.RankChange.Value)}",
        _ => "0"
    };

    var timeChange = point.TimeChange switch
    {
        null => "-",
        _ => FormatTimeChange(point.TimeChange.Value)
    };

    Console.WriteLine(
        $"{point.RankingDate:dd/MM/yyyy}   " +
        $"#{point.Rank,-4}   " +
        $"{FormatRaceTime(point.Time),-10}   " +
        $"{rankChange,-6}   " +
        $"{timeChange}");
}


// ========================================
// RESUME GLOBAL
// ========================================

Console.WriteLine();

Console.WriteLine(
    $"Progression totale : " +
    $"{FormatRankChange(evolution.TotalRankChange)}");

Console.WriteLine(
    $"Evolution du chrono : " +
    $"{FormatTimeChange(evolution.TotalTimeChange)}");


// ========================================
// COMPARAISON DE LA DERNIERE SEMAINE
// ========================================

var previousDate = new DateOnly(2026, 9, 15);
var currentDate = new DateOnly(2026, 9, 22);

var comparisonService =
    new MkddSnapshotComparisonService(db);

var comparison =
    await comparisonService.CompareAsync(
        "Mattilde F",
        courseName,
        previousDate,
        currentDate);

if (comparison is null)
{
    Console.WriteLine(
        "Impossible de comparer les snapshots.");

    return;
}

Console.WriteLine();
Console.WriteLine("Analyse du mouvement");
Console.WriteLine("---------------------");

Console.WriteLine(
    $"Classement : #{comparison.PreviousRank} " +
    $"-> #{comparison.CurrentRank}");

Console.WriteLine(
    $"Chrono : " +
    $"{FormatRaceTime(comparison.PreviousTime)} " +
    $"-> " +
    $"{FormatRaceTime(comparison.CurrentTime)}");

Console.WriteLine();

Console.WriteLine(
    $"Classement theorique avec son nouveau chrono : " +
    $"#{comparison.CounterfactualRank}");

Console.WriteLine();

Console.WriteLine(
    $"Progression totale : " +
    $"{FormatRankChange(comparison.TotalPlacesGained)}");

Console.WriteLine(
    $"Grace a son amelioration : " +
    $"{FormatRankChange(comparison.PlacesGainedFromOwnImprovement)}");

Console.WriteLine(
    $"Grace aux autres joueurs : " +
    $"{FormatRankChange(comparison.PlacesGainedFromOthers)}");

static string FormatRankChange(int change)
{
    if (change > 0)
    {
        var word = change == 1 ? "place" : "places";
        return $"+{change} {word}";
    }

    if (change < 0)
    {
        var absolute = Math.Abs(change);
        var word = absolute == 1 ? "place" : "places";
        return $"-{absolute} {word}";
    }

    return "— aucune progression";
}

static string FormatRaceTime(TimeSpan time)
{
    return $"{(int)time.TotalMinutes}:{time.Seconds:00}.{time.Milliseconds:000}";
}

static string FormatTimeChange(TimeSpan change)
{
    var sign = change < TimeSpan.Zero ? "-" : "+";
    var absolute = change.Duration();

    return $"{sign}{absolute.TotalMilliseconds:0} ms";
}