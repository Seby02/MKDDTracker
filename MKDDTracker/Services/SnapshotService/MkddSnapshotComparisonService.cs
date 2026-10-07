using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotComparisonService
{
    private readonly MkddDbContext _db;

    public MkddSnapshotComparisonService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<MkddRankMovementAnalysis?> CompareAsync(
        string playerName,
        string courseName,
        DateOnly previousDate,
        DateOnly currentDate)
    {
        var player = await _db.Players
            .SingleOrDefaultAsync(x => x.Name == playerName);

        if (player is null)
            return null;

        var course = await _db.Courses
            .SingleOrDefaultAsync(x => x.Name == courseName);

        if (course is null)
            return null;

        var previousSnapshot = await _db.Snapshots
            .Include(x => x.Performances)
                .ThenInclude(x => x.Player)
            .SingleOrDefaultAsync(x =>
                x.CourseId == course.Id &&
                x.RankingDate == previousDate);

        var currentSnapshot = await _db.Snapshots
            .Include(x => x.Performances)
                .ThenInclude(x => x.Player)
            .SingleOrDefaultAsync(x =>
                x.CourseId == course.Id &&
                x.RankingDate == currentDate);

        if (previousSnapshot is null ||
            currentSnapshot is null)
        {
            return null;
        }

        var previousPerformance =
            previousSnapshot.Performances
                .SingleOrDefault(x => x.PlayerId == player.Id);

        var currentPerformance =
            currentSnapshot.Performances
                .SingleOrDefault(x => x.PlayerId == player.Id);

        if (previousPerformance is null ||
            currentPerformance is null)
        {
            return null;
        }

        // -------------------------------------------------
        // 1. Classement théorique avec le nouveau chrono
        // -------------------------------------------------

        var counterfactualRank =
    CalculateRank(
        previousSnapshot.Performances,
        player.Id,
        currentPerformance.Time);

        // -------------------------------------------------
        // 2. Progression totale
        // -------------------------------------------------

        var totalPlacesGained =
            previousPerformance.Rank -
            currentPerformance.Rank;

        // -------------------------------------------------
        // 3. Progression grâce à son propre chrono
        // -------------------------------------------------

        var placesGainedFromOwnImprovement =
            previousPerformance.Rank -
            counterfactualRank;

        // -------------------------------------------------
        // 4. Progression grâce aux autres joueurs
        // -------------------------------------------------

        var placesGainedFromOthers =
            counterfactualRank -
            currentPerformance.Rank;

        // -------------------------------------------------
        // 5. Analyse des autres joueurs
        // -------------------------------------------------

        var playersWhoMoved =
    BuildPlayerMovements(
        player.Id,
        previousSnapshot,
        currentSnapshot);

        Console.WriteLine();
        Console.WriteLine("DEBUG MOUVEMENTS DETECTES");

        foreach (var movement in playersWhoMoved)
        {
            Console.WriteLine(
                $"{movement.PlayerName} : " +
                $"{movement.PreviousRank} -> {movement.CurrentRank} | " +
                $"{movement.Relation}");
        }

        var enzoMovement =
    playersWhoMoved
        .SingleOrDefault(x =>
            x.PlayerName == "Enzo Vusur");

        Console.WriteLine(
            enzoMovement is null
                ? "DEBUG ENZO : PAS DE MOUVEMENT"
                : $"DEBUG ENZO : " +
                  $"{enzoMovement.PreviousRank} -> " +
                  $"{enzoMovement.CurrentRank} | " +
                  $"{enzoMovement.Relation}");

        Console.WriteLine();
        Console.WriteLine("DEBUG AUTOUR DE MATTLIDE");

        foreach (var performance in currentSnapshot.Performances
            .OrderBy(x => x.Rank)
            .Where(x =>
                x.Rank >= currentPerformance.Rank - 3 &&
                x.Rank <= currentPerformance.Rank + 3))
        {
            Console.WriteLine(
                $"#{performance.Rank} - " +
                $"{performance.Player.Name} - " +
                $"{performance.Time}");
        }

        var rankImpacts =
    BuildRankImpacts(
        player.Id,
        previousSnapshot,
        currentSnapshot,
        currentPerformance.Time);

        return new MkddRankMovementAnalysis(
            player.Name,
            course.Name,

            previousDate,
            currentDate,

            previousPerformance.Rank,
            currentPerformance.Rank,

            previousPerformance.Time,
            currentPerformance.Time,

            counterfactualRank,

            totalPlacesGained,
            placesGainedFromOwnImprovement,
            placesGainedFromOthers,

            playersWhoMoved,
            rankImpacts);
    }

    private static IReadOnlyList<MkddRankImpact> BuildRankImpacts(
    int targetPlayerId,
    MkddSnapshot previousSnapshot,
    MkddSnapshot currentSnapshot,
    TimeSpan targetCurrentTime)
    {
        var previousByPlayer =
            previousSnapshot.Performances
                .Where(x => x.PlayerId != targetPlayerId)
                .ToDictionary(x => x.PlayerId);

        var currentByPlayer =
            currentSnapshot.Performances
                .Where(x => x.PlayerId != targetPlayerId)
                .ToDictionary(x => x.PlayerId);

        var impacts = new List<MkddRankImpact>();

        foreach (var playerId in previousByPlayer.Keys)
        {
            if (!currentByPlayer.TryGetValue(
                    playerId,
                    out var current))
            {
                continue;
            }

            var previous = previousByPlayer[playerId];

            var wasAhead =
                previous.Time < targetCurrentTime;

            var isAhead =
                current.Time < targetCurrentTime;

            if (wasAhead == isAhead)
                continue;

            var impact =
                wasAhead && !isAhead
                    ? 1
                    : -1;

            impacts.Add(
                new MkddRankImpact(
                    playerId,
                    previous.Player.Name,
                    previous.Rank,
                    current.Rank,
                    previous.Time,
                    current.Time,
                    impact));
        }

        return impacts
            .OrderByDescending(x => x.RankImpact)
            .ThenBy(x => x.CurrentRank)
            .ToList();
    }

    private static IReadOnlyList<MkddPlayerMovement> BuildPlayerMovements(
    int targetPlayerId,
    MkddSnapshot previousSnapshot,
    MkddSnapshot currentSnapshot)
    {
        var targetPrevious =
            previousSnapshot.Performances
                .Single(x => x.PlayerId == targetPlayerId);

        var targetCurrent =
            currentSnapshot.Performances
                .Single(x => x.PlayerId == targetPlayerId);

        var previousByPlayer =
            previousSnapshot.Performances
                .Where(x => x.PlayerId != targetPlayerId)
                .ToDictionary(x => x.PlayerId);

        var currentByPlayer =
            currentSnapshot.Performances
                .Where(x => x.PlayerId != targetPlayerId)
                .ToDictionary(x => x.PlayerId);

        var movements = new List<MkddPlayerMovement>();

        foreach (var playerId in previousByPlayer.Keys)
        {
            if (previousByPlayer[playerId].Player.Name == "Enzo Vusur")
            {
                var previous = previousByPlayer[playerId];

                Console.WriteLine(
                    $"DEBUG ENZO PREVIOUS : " +
                    $"PlayerId={playerId}, " +
                    $"Rank={previous.Rank}, " +
                    $"Name={previous.Player.Name}");

                if (!currentByPlayer.TryGetValue(
                        playerId,
                        out var enzoCurrent))
                {
                    Console.WriteLine(
                        "DEBUG ENZO : ABSENT DE currentByPlayer");
                }
                else
                {
                    Console.WriteLine(
                        $"DEBUG ENZO CURRENT : " +
                        $"PlayerId={enzoCurrent.PlayerId}, " +
                        $"Rank={enzoCurrent.Rank}, " +
                        $"Name={enzoCurrent.Player.Name}");

                    var wasAheadBefore =
                        previous.Rank < targetPrevious.Rank;

                    var isAheadNow =
                        enzoCurrent.Rank < targetCurrent.Rank;

                    Console.WriteLine(
                        $"DEBUG ENZO RELATION : " +
                        $"wasAheadBefore={wasAheadBefore}, " +
                        $"isAheadNow={isAheadNow}");
                }
            }

            // ... puis ton code actuel
        }

        return movements
            .Where(x =>
                x.Relation !=
                MkddMovementRelation.NoDirectCrossing)
            .OrderBy(x => x.CurrentRank)
            .ToList();
    }

    private static int CalculateRank(
    IEnumerable<MkddPerformanceEntity> performances,
    int targetPlayerId,
    TimeSpan targetTime)
    {
        return performances
            .Where(x => x.PlayerId != targetPlayerId)
            .Count(x =>
                x.Time < targetTime ||
                (x.Time == targetTime &&
                 x.PlayerId < targetPlayerId))
            + 1;
    }
}