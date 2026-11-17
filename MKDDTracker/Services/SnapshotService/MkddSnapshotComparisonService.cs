using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.Ranking;
using MovementAnalysis = MKDDTracker.Scraper.Models.Evolution.MkddRankMovementAnalysis;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotComparisonService
{
    private readonly MkddDbContext _db;

    public MkddSnapshotComparisonService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<MovementAnalysis?> CompareAsync(
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

        if (previousSnapshot is null || currentSnapshot is null)
            return null;

        var previousPerformance = previousSnapshot.Performances
            .SingleOrDefault(x => x.PlayerId == player.Id);

        var currentPerformance = currentSnapshot.Performances
            .SingleOrDefault(x => x.PlayerId == player.Id);

        // A comparison requires the target player to exist in both snapshots.
        // Entries and removals of the other players are represented as movements.
        if (previousPerformance is null || currentPerformance is null)
            return null;

        var counterfactualRank =
            MkddRankingCalculator.CalculateRank(
                previousSnapshot.Performances
                    .Where(x => x.PlayerId != player.Id)
                    .Select(x => x.Time),
                currentPerformance.Time);

        var totalPlacesGained =
            previousPerformance.Rank - currentPerformance.Rank;

        var placesGainedFromOwnImprovement =
            previousPerformance.Rank - counterfactualRank;

        var placesGainedFromOthers =
            counterfactualRank - currentPerformance.Rank;

        var playersWhoMoved = BuildPlayerMovements(
            player.Id,
            previousSnapshot,
            currentSnapshot);

        var rankImpacts = BuildRankImpacts(
            player.Id,
            previousSnapshot,
            currentSnapshot,
            currentPerformance.Time);

        return new MovementAnalysis(
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
        var previousByPlayer = previousSnapshot.Performances
            .Where(x => x.PlayerId != targetPlayerId)
            .ToDictionary(x => x.PlayerId);

        var currentByPlayer = currentSnapshot.Performances
            .Where(x => x.PlayerId != targetPlayerId)
            .ToDictionary(x => x.PlayerId);

        var impacts = new List<MkddRankImpact>();

        foreach (var playerId in previousByPlayer.Keys)
        {
            if (!currentByPlayer.TryGetValue(playerId, out var current))
                continue;

            var previous = previousByPlayer[playerId];
            var wasAhead = previous.Time < targetCurrentTime;
            var isAhead = current.Time < targetCurrentTime;

            if (wasAhead == isAhead)
                continue;

            impacts.Add(
                new MkddRankImpact(
                    playerId,
                    previous.Player.Name,
                    previous.Rank,
                    current.Rank,
                    previous.Time,
                    current.Time,
                    wasAhead && !isAhead ? 1 : -1));
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
        var targetPrevious = previousSnapshot.Performances
            .Single(x => x.PlayerId == targetPlayerId);

        var targetCurrent = currentSnapshot.Performances
            .Single(x => x.PlayerId == targetPlayerId);

        var previousByPlayer = previousSnapshot.Performances
            .Where(x => x.PlayerId != targetPlayerId)
            .ToDictionary(x => x.PlayerId);

        var currentByPlayer = currentSnapshot.Performances
            .Where(x => x.PlayerId != targetPlayerId)
            .ToDictionary(x => x.PlayerId);

        var playerIds = previousByPlayer.Keys
            .Union(currentByPlayer.Keys)
            .OrderBy(x => x);

        var movements = new List<MkddPlayerMovement>();

        foreach (var playerId in playerIds)
        {
            var existedBefore = previousByPlayer.TryGetValue(
                playerId,
                out var previous);

            var existsNow = currentByPlayer.TryGetValue(
                playerId,
                out var current);

            if (!existedBefore)
            {
                movements.Add(
                    new MkddPlayerMovement(
                        playerId,
                        current!.Player.Name,
                        previousSnapshot.RankingDate,
                        currentSnapshot.RankingDate,
                        0,
                        current.Rank,
                        TimeSpan.Zero,
                        current.Time,
                        0,
                        MkddMovementRelation.NewEntry,
                        MkddMovementCause.NewEntry)
                    {
                        RelatedPlayerId = targetPlayerId,
                        WasPresentBefore = false
                    });

                continue;
            }

            if (!existsNow)
            {
                movements.Add(
                    new MkddPlayerMovement(
                        playerId,
                        previous!.Player.Name,
                        previousSnapshot.RankingDate,
                        currentSnapshot.RankingDate,
                        previous.Rank,
                        0,
                        previous.Time,
                        TimeSpan.Zero,
                        0,
                        MkddMovementRelation.PlayerRemoved,
                        MkddMovementCause.PlayerRemoved)
                    {
                        RelatedPlayerId = targetPlayerId,
                        IsPresentNow = false
                    });

                continue;
            }

            var relation = DetermineRelation(
                targetPrevious.Time,
                targetCurrent.Time,
                previous!.Time,
                current!.Time);

            if (relation == MkddMovementRelation.NoDirectCrossing)
                continue;

            movements.Add(
                new MkddPlayerMovement(
                    playerId,
                    current.Player.Name,
                    previousSnapshot.RankingDate,
                    currentSnapshot.RankingDate,
                    previous.Rank,
                    current.Rank,
                    previous.Time,
                    current.Time,
                    previous.Rank - current.Rank,
                    relation,
                    DetermineCause(
                        targetPrevious.Time,
                        targetCurrent.Time,
                        previous.Time,
                        current.Time))
                {
                    RelatedPlayerId = targetPlayerId
                });
        }

        return movements
            .OrderBy(x => x.CurrentRank == 0 ? int.MaxValue : x.CurrentRank)
            .ThenBy(x => x.PreviousRank == 0 ? int.MaxValue : x.PreviousRank)
            .ThenBy(x => x.PlayerName, StringComparer.Ordinal)
            .ToList();
    }

    private static MkddMovementRelation DetermineRelation(
        TimeSpan targetPreviousTime,
        TimeSpan targetCurrentTime,
        TimeSpan opponentPreviousTime,
        TimeSpan opponentCurrentTime)
    {
        var previousRelation = targetPreviousTime.CompareTo(opponentPreviousTime);
        var currentRelation = targetCurrentTime.CompareTo(opponentCurrentTime);

        if (previousRelation != 0 && currentRelation == 0)
            return MkddMovementRelation.TieCreated;

        if (previousRelation == 0 && currentRelation != 0)
            return MkddMovementRelation.TieBroken;

        if (previousRelation > 0 && currentRelation < 0)
            return MkddMovementRelation.TargetPassedPlayer;

        if (previousRelation < 0 && currentRelation > 0)
            return MkddMovementRelation.PlayerPassedTarget;

        return MkddMovementRelation.NoDirectCrossing;
    }

    private static MkddMovementCause DetermineCause(
        TimeSpan targetPreviousTime,
        TimeSpan targetCurrentTime,
        TimeSpan opponentPreviousTime,
        TimeSpan opponentCurrentTime)
    {
        var targetImproved = targetCurrentTime < targetPreviousTime;
        var opponentImproved = opponentCurrentTime < opponentPreviousTime;
        var targetRegressed = targetCurrentTime > targetPreviousTime;
        var opponentRegressed = opponentCurrentTime > opponentPreviousTime;

        if (targetImproved && opponentImproved)
            return MkddMovementCause.BothImproved;

        if (targetRegressed && opponentRegressed)
            return MkddMovementCause.BothRegressed;

        if (targetImproved)
            return MkddMovementCause.TargetDriven;

        if (opponentImproved)
            return MkddMovementCause.OpponentDriven;

        if (targetRegressed)
            return MkddMovementCause.TargetRegressed;

        if (opponentRegressed)
            return MkddMovementCause.OpponentRegressed;

        return MkddMovementCause.NoTimeChange;
    }
}
