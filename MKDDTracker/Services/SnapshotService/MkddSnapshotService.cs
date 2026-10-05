using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotService
{
    private readonly MkddDbContext _db;

    public MkddSnapshotService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<MkddSnapshot> SaveSnapshotAsync(
        string courseName,
        IReadOnlyList<MkddPerformance> performances,
        DateTime capturedAt)
    {
        var course = await _db.Courses
            .SingleOrDefaultAsync(x => x.Name == courseName);

        if (course is null)
        {
            course = new MkddCourse
            {
                Name = courseName
            };

            _db.Courses.Add(course);
            await _db.SaveChangesAsync();
        }

        var snapshot = new MkddSnapshot
        {
            CourseId = course.Id,
            CapturedAt = capturedAt
        };

        _db.Snapshots.Add(snapshot);

        foreach (var performance in performances)
        {
            var player = await _db.Players
                .SingleOrDefaultAsync(
                    x => x.SitePlayerId == performance.SitePlayerId);

            if (player is null)
            {
                player = new MkddPlayer
                {
                    SitePlayerId = performance.SitePlayerId,
                    Name = performance.PlayerName,
                    Country = performance.Country
                };

                _db.Players.Add(player);
            }
            else
            {
                // Le nom ou le pays peuvent éventuellement évoluer.
                player.Name = performance.PlayerName;
                player.Country = performance.Country;
            }

            snapshot.Performances.Add(
                new MkddPerformanceEntity
                {
                    Player = player,
                    Rank = performance.Rank,
                    Time = performance.Time,
                    Standard = performance.Standard,
                    RecordDate = performance.RecordDate,
                    VideoUrl = performance.VideoUrl
                });
        }

        await _db.SaveChangesAsync();

        return snapshot;
    }
}