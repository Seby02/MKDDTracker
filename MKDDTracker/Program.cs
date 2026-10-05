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

var evolutionService = new MkddEvolutionService(db);

var evolution =
    await evolutionService.GetPlayerEvolutionAsync(
        "Mattilde F",
        courseName);

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
        > 0 => $"▲ {point.RankChange}",
        < 0 => $"▼ {Math.Abs(point.RankChange.Value)}",
        _ => "—"
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

    Console.WriteLine();

    Console.WriteLine(
        $"Progression totale : {FormatRankChange(evolution.TotalRankChange)}");

    Console.WriteLine(
        $"Évolution du chrono : {FormatTimeChange(evolution.TotalTimeChange)}");
}

static string FormatRankChange(int change)
{
    if (change > 0)
    {
        return $"▲ +{change} places";
    }

    if (change < 0)
    {
        return $"▼ {Math.Abs(change)} places";
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