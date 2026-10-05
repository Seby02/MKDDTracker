using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddEvolutionService
{
    private readonly MkddDbContext _db;

    public MkddEvolutionService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<MkddPlayerEvolution?> GetPlayerEvolutionAsync(
        string playerName,
        string courseName)
    {
        var player = await _db.Players
            .SingleOrDefaultAsync(x => x.Name == playerName);

        if (player is null)
        {
            return null;
        }

        var course = await _db.Courses
            .SingleOrDefaultAsync(x => x.Name == courseName);

        if (course is null)
        {
            return null;
        }

        var performances = await _db.Performances
            .Include(x => x.Snapshot)
            .Where(x =>
                x.PlayerId == player.Id &&
                x.Snapshot.CourseId == course.Id)
            .OrderBy(x => x.Snapshot.CapturedAt)
            .ToListAsync();

        if (performances.Count == 0)
        {
            return null;
        }

        var points = new List<MkddPlayerEvolutionPoint>();

        MkddPerformanceEntity? previous = null;

        foreach (var performance in performances)
        {
            int? rankChange = null;
            TimeSpan? timeChange = null;

            if (previous is not null)
            {
                rankChange =
                    previous.Rank - performance.Rank;

                timeChange =
                    performance.Time - previous.Time;
            }

            points.Add(
                new MkddPlayerEvolutionPoint(
                    performance.Snapshot.CapturedAt,
                    performance.Rank,
                    performance.Time,
                    rankChange,
                    timeChange));

            previous = performance;
        }

        return new MkddPlayerEvolution(
            player.Name,
            course.Name,
            points);
    }
}