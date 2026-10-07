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

            playersWhoMoved);
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
            if (!currentByPlayer.TryGetValue(
                    playerId,
                    out var current))
            {
                continue;
            }

            var previous =
                previousByPlayer[playerId];

            var wasAheadBefore =
                previous.Rank < targetPrevious.Rank;

            var isAheadNow =
                current.Rank < targetCurrent.Rank;

            MkddMovementRelation relation;

            if (wasAheadBefore && !isAheadNow)
            {
                relation =
                    MkddMovementRelation.TargetPassedPlayer;
            }
            else if (!wasAheadBefore && isAheadNow)
            {
                relation =
                    MkddMovementRelation.PlayerPassedTarget;
            }
            else
            {
                relation =
                    MkddMovementRelation.NoDirectCrossing;
            }

            var targetImproved =
    targetCurrent.Time < targetPrevious.Time;

            var opponentImproved =
                current.Time < previous.Time;

            MkddMovementCause cause;

            if (targetImproved && opponentImproved)
            {
                cause = MkddMovementCause.BothImproved;
            }
            else if (targetImproved)
            {
                cause = MkddMovementCause.TargetDriven;
            }
            else
            {
                cause = MkddMovementCause.OpponentDriven;
            }

            movements.Add(
                new MkddPlayerMovement(
                    playerId,
                    previous.Player.Name,
                    previousSnapshot.RankingDate,
                    currentSnapshot.RankingDate,
                    previous.Rank,
                    current.Rank,
                    previous.Time,
                    current.Time,
                    previous.Rank - current.Rank,
                    relation,
                    cause));
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