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

await using var db = new MkddDbContext();

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
// EVOLUTION DU JOUEUR
// ========================================

var evolutionService =
    new MkddEvolutionService(db);

var evolution =
    await evolutionService.GetPlayerEvolutionAsync(
        playerName,
        courseName,
        evolutionStartDate,
        evolutionEndDate);

if (evolution is null)
{
    Console.WriteLine(
        "Joueur introuvable.");

    return;
}


// ========================================
// HISTORIQUE
// ========================================

Console.WriteLine();

Console.WriteLine(
    $"{evolution.PlayerName} - " +
    $"{evolution.CourseName}");

Console.WriteLine(
    "------------------------------------------------");

foreach (var point in evolution.Points)
{
    var rankChange = point.RankChange switch
    {
        null => "-",

        > 0 =>
            $"+{point.RankChange}",

        < 0 =>
            $"-{Math.Abs(point.RankChange.Value)}",

        _ => "0"
    };

    var timeChange = point.TimeChange switch
    {
        null => "-",

        _ =>
            FormatTimeChange(
                point.TimeChange.Value)
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
    "RESUME GLOBAL");

Console.WriteLine(
    "-------------");

Console.WriteLine(
    $"Progression totale : " +
    $"{FormatRankChange(evolution.TotalRankChange)}");

Console.WriteLine(
    $"Evolution du chrono : " +
    $"{FormatTimeChange(evolution.TotalTimeChange)}");


// ========================================
// SERVICE DE COMPARAISON
// ========================================

var comparisonService =
    new MkddSnapshotComparisonService(db);


// ========================================
// COMPARAISON DE LA DERNIERE SEMAINE
// ========================================

var comparison =
    await comparisonService.CompareAsync(
        playerName,
        courseName,
        previousDate,
        currentDate);

if (comparison is null)
{
    Console.WriteLine();

    Console.WriteLine(
        "Impossible de comparer " +
        "les snapshots.");

    return;
}


// ========================================
// ANALYSE DU MOUVEMENT
// ========================================

Console.WriteLine();

Console.WriteLine(
    "ANALYSE DU MOUVEMENT");

Console.WriteLine(
    "---------------------");

Console.WriteLine(
    $"Classement : " +
    $"#{comparison.PreviousRank} " +
    $"-> " +
    $"#{comparison.CurrentRank}");

Console.WriteLine(
    $"Chrono : " +
    $"{FormatRaceTime(comparison.PreviousTime)} " +
    $"-> " +
    $"{FormatRaceTime(comparison.CurrentTime)}");

Console.WriteLine();

Console.WriteLine(
    $"Classement theorique " +
    $"avec son nouveau chrono : " +
    $"#{comparison.CounterfactualRank}");

Console.WriteLine();

Console.WriteLine(
    $"Progression totale : " +
    $"{FormatRankChange(comparison.TotalPlacesGained)}");

Console.WriteLine(
    $"Grace a son amelioration : " +
    $"{FormatRankChange(
        comparison.PlacesGainedFromOwnImprovement)}");

Console.WriteLine(
    $"Grace aux autres joueurs : " +
    $"{FormatRankChange(
        comparison.PlacesGainedFromOthers)}");


// ========================================
// JOUEURS AYANT CROISE LE JOUEUR CIBLE
// ========================================

Console.WriteLine();

Console.WriteLine(
    "JOUEURS RESPONSABLES DES MOUVEMENTS");

Console.WriteLine(
    "-----------------------------------");

var crossingPlayers =
    comparison.PlayersWhoMoved
        .Where(x =>
            x.Relation !=
            MkddMovementRelation.NoDirectCrossing)
        .ToList();

if (crossingPlayers.Count == 0)
{
    Console.WriteLine(
        "Aucun joueur n'a directement " +
        "croise le joueur cible.");
}
else
{
    foreach (var movement in crossingPlayers)
    {
        switch (movement.Relation)
        {
            case MkddMovementRelation.TargetPassedPlayer:

                Console.WriteLine(
                    $"+{1} place : " +
                    $"{movement.PlayerName} " +
                    $"#{movement.PreviousRank} " +
                    $"-> #{movement.CurrentRank}");

                Console.WriteLine(
                    "  -> " +
                    $"{playerName} le depasse");

                break;

            case MkddMovementRelation.PlayerPassedTarget:

                Console.WriteLine(
                    $"-{1} place : " +
                    $"{movement.PlayerName} " +
                    $"#{movement.PreviousRank} " +
                    $"-> #{movement.CurrentRank}");

                Console.WriteLine(
                    "  -> " +
                    $"{movement.PlayerName} depasse " +
                    $"{playerName}");

                break;
        }
    }
}


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
        "Evolution complete introuvable.");

    return;
}


// ========================================
// ANALYSE SEMAINE PAR SEMAINE
// ========================================

Console.WriteLine();

Console.WriteLine(
    "EVOLUTION SEMAINE PAR SEMAINE");

Console.WriteLine(
    "------------------------------");

foreach (var step in completeEvolution.Steps)
{
    Console.WriteLine();

    Console.WriteLine(
        $"{step.PreviousDate:dd/MM/yyyy} " +
        $"-> " +
        $"{step.CurrentDate:dd/MM/yyyy}");

    Console.WriteLine(
        $"Classement : " +
        $"#{step.PreviousRank} " +
        $"-> " +
        $"#{step.CurrentRank}   " +
        $"{FormatRankChange(
            step.TotalPlacesGained)}");

    Console.WriteLine(
        $"Chrono     : " +
        $"{FormatRaceTime(step.PreviousTime)} " +
        $"-> " +
        $"{FormatRaceTime(step.CurrentTime)}   " +
        $"{FormatTimeChange(
            step.CurrentTime -
            step.PreviousTime)}");

    Console.WriteLine(
        $"  Son chrono : " +
        $"{FormatRankChange(
            step.PlacesGainedFromOwnImprovement)}");

    Console.WriteLine(
        $"  Autres     : " +
        $"{FormatRankChange(
            step.PlacesGainedFromOthers)}");
}


// ========================================
// RESUME EVOLUTION COMPLETE
// ========================================

Console.WriteLine();

Console.WriteLine(
    "RESUME DE L'EVOLUTION");

Console.WriteLine(
    "---------------------");

Console.WriteLine(
    $"Joueur : " +
    $"{completeEvolution.PlayerName}");

Console.WriteLine(
    $"Circuit : " +
    $"{completeEvolution.CourseName}");

Console.WriteLine();

Console.WriteLine(
    $"Progression totale : " +
    $"{FormatRankChange(
        completeEvolution.TotalPlacesGained)}");

Console.WriteLine(
    $"Grace a son chrono : " +
    $"{FormatRankChange(
        completeEvolution
            .TotalPlacesGainedFromOwnImprovement)}");

Console.WriteLine(
    $"Grace aux autres : " +
    $"{FormatRankChange(
        completeEvolution
            .TotalPlacesGainedFromOthers)}");

Console.WriteLine(
    $"Evolution du chrono : " +
    $"{FormatTimeChange(
        completeEvolution.TotalTimeChange)}");


// ========================================
// FORMATTAGE
// ========================================

static string FormatRankChange(int change)
{
    if (change > 0)
    {
        var word =
            change == 1
                ? "place"
                : "places";

        return $"+{change} {word}";
    }

    if (change < 0)
    {
        var absolute =
            Math.Abs(change);

        var word =
            absolute == 1
                ? "place"
                : "places";

        return $"-{absolute} {word}";
    }

    return "— aucune progression";
}


static string FormatRaceTime(TimeSpan time)
{
    return
        $"{(int)time.TotalMinutes}:" +
        $"{time.Seconds:00}." +
        $"{time.Milliseconds:000}";
}


static string FormatTimeChange(TimeSpan change)
{
    var sign =
        change < TimeSpan.Zero
            ? "-"
            : "+";

    var absolute =
        change.Duration();

    return
        $"{sign}" +
        $"{absolute.TotalMilliseconds:0} ms";
}