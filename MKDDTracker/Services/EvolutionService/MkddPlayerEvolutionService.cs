using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Models.Evolution;
using MKDDTracker.Scraper.Services.SnapshotService;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddPlayerEvolutionService
{
    private readonly MkddDbContext _db;
    private readonly MkddSnapshotComparisonService _comparisonService;

    public MkddPlayerEvolutionService(
        MkddDbContext db,
        MkddSnapshotComparisonService comparisonService)
    {
        _db = db;
        _comparisonService = comparisonService;
    }

    public async Task<MkddCompletePlayerEvolution?> AnalyzeAsync(
        string playerName,
        string courseName,
        DateOnly startDate,
        DateOnly endDate)
    {
        var player = await _db.Players
            .SingleOrDefaultAsync(x => x.Name == playerName);

        if (player is null)
            return null;

        var course = await _db.Courses
            .SingleOrDefaultAsync(x => x.Name == courseName);

        if (course is null)
            return null;

        var snapshots = await _db.Snapshots
            .Where(x =>
                x.CourseId == course.Id &&
                x.RankingDate >= startDate &&
                x.RankingDate <= endDate)
            .OrderBy(x => x.RankingDate)
            .Select(x => x.RankingDate)
            .ToListAsync();

        if (snapshots.Count < 2)
            return null;

        var steps = new List<MkddPlayerEvolutionStep>();

        for (var i = 1; i < snapshots.Count; i++)
        {
            var previousDate = snapshots[i - 1];
            var currentDate = snapshots[i];

            var comparison =
                await _comparisonService.CompareAsync(
                    playerName,
                    courseName,
                    previousDate,
                    currentDate);

            if (comparison is null)
                continue;

            steps.Add(
    new MkddPlayerEvolutionStep(
        previousDate,
        currentDate,
        comparison.PreviousRank,
        comparison.CurrentRank,
        comparison.PreviousTime,
        comparison.CurrentTime,
        comparison.CounterfactualRank,
        comparison.TotalPlacesGained,
        comparison.PlacesGainedFromOwnImprovement,
        comparison.PlacesGainedFromOthers,
        comparison.PlayersWhoMoved));
        }

        if (steps.Count == 0)
            return null;

        return new MkddCompletePlayerEvolution(
            player.Name,
            course.Name,
            steps);
    }
}