using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Models;

namespace MKDDTracker.Scraper.Services;

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
            .SingleOrDefaultAsync(x =>
                x.CourseId == course.Id &&
                x.RankingDate == previousDate);

        var currentSnapshot = await _db.Snapshots
            .Include(x => x.Performances)
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
                .SingleOrDefault(x =>
                    x.PlayerId == player.Id);

        var currentPerformance =
            currentSnapshot.Performances
                .SingleOrDefault(x =>
                    x.PlayerId == player.Id);

        if (previousPerformance is null ||
            currentPerformance is null)
        {
            return null;
        }

        var previousOtherPlayers =
            previousSnapshot.Performances
                .Where(x => x.PlayerId != player.Id)
                .ToList();

        // Classement théorique :
        //
        // On garde tous les chronos des autres joueurs
        // tels qu'ils étaient la semaine précédente,
        // mais on donne à Mattilde son nouveau chrono.
        var counterfactualRank =
            1 + previousOtherPlayers.Count(x =>
                x.Time < currentPerformance.Time);

        var totalPlacesGained =
            previousPerformance.Rank -
            currentPerformance.Rank;

        var placesGainedFromOwnImprovement =
            previousPerformance.Rank -
            counterfactualRank;

        var placesGainedFromOthers =
            counterfactualRank -
            currentPerformance.Rank;

        return new MkddRankMovementAnalysis(
            previousPerformance.Rank,
            currentPerformance.Rank,
            counterfactualRank,
            totalPlacesGained,
            placesGainedFromOwnImprovement,
            placesGainedFromOthers,
            previousPerformance.Time,
            currentPerformance.Time);
    }
}