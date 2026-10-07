using Microsoft.EntityFrameworkCore;
using MKDDTracker.Scraper.Data;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;

namespace MKDDTracker.Scraper.Services;

public sealed class MkddTestDataSeeder
{
    private readonly MkddDbContext _db;

    public MkddTestDataSeeder(MkddDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(
    string courseName,
    string mattildeName,
    string enzoName,
    IReadOnlyList<MkddPerformance> originalPerformances)
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

        var mattilde = await _db.Players
            .SingleAsync(x => x.Name == mattildeName);

        var enzo = await _db.Players
            .SingleAsync(x => x.Name == enzoName);

        var dates = new[]
        {
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 8),
            new DateOnly(2026, 9, 15),
            new DateOnly(2026, 9, 22)
        };

        foreach (var date in dates)
        {
            var exists = await _db.Snapshots
                .AnyAsync(x =>
                    x.CourseId == course.Id &&
                    x.RankingDate == date);

            if (exists)
                continue;

            var snapshot = new MkddSnapshot
            {
                CourseId = course.Id,
                RankingDate = date,
                CapturedAt = date.ToDateTime(
                    new TimeOnly(12, 0))
            };

            var temporaryPerformances = new List<(int PlayerId, TimeSpan Time, DateOnly RecordDate)>();

            foreach (var original in originalPerformances)
            {
                var player = await _db.Players
                    .SingleOrDefaultAsync(x =>
                        x.SitePlayerId == original.SitePlayerId);

                if (player is null)
                    continue;

                var time = original.Time;

                // -----------------------------------------
                // MATTILDE
                // -----------------------------------------

                if (player.Id == mattilde.Id)
                {
                    switch (date)
                    {
                        case { Year: 2026, Month: 9, Day: 1 }:
                            time = TimeSpan.FromMilliseconds(74_642);
                            break;

                        case { Year: 2026, Month: 9, Day: 8 }:
                            time = TimeSpan.FromMilliseconds(74_510);
                            break;

                        case { Year: 2026, Month: 9, Day: 15 }:
                            time = TimeSpan.FromMilliseconds(74_401);
                            break;

                        case { Year: 2026, Month: 9, Day: 22 }:
                            time = TimeSpan.FromMilliseconds(74_210);
                            break;
                    }
                }

                // -----------------------------------------
                // ENZO
                // -----------------------------------------

                // Enzo sert ici à simuler un joueur qui était devant Mattilde
                // puis qui passe derrière elle.
                if (player.Id == enzo.Id)
                {
                    switch (date)
                    {
                        case { Year: 2026, Month: 9, Day: 1 }:
                            time = TimeSpan.FromMilliseconds(74_500);
                            break;

                        case { Year: 2026, Month: 9, Day: 8 }:
                            time = TimeSpan.FromMilliseconds(74_700);
                            break;

                        case { Year: 2026, Month: 9, Day: 15 }:
                            time = TimeSpan.FromMilliseconds(74_200);
                            break;

                        case { Year: 2026, Month: 9, Day: 22 }:
                            time = TimeSpan.FromMilliseconds(74_500);
                            break;
                    }
                }

                // -----------------------------------------
                // AUTRES JOUEURS
                // -----------------------------------------

                if (player.Id != mattilde.Id &&
                    player.Id != enzo.Id)
                {
                    // Le joueur actuellement autour de Mattilde
                    // va volontairement ralentir pendant la dernière semaine.
                    //
                    // On identifie ici le joueur qui avait le rang 3
                    // dans les données originales.
                    if (original.Rank == 3 &&
                        date == new DateOnly(2026, 9, 22))
                    {
                        time = TimeSpan.FromMilliseconds(74_500);
                    }
                }

                temporaryPerformances.Add(
    (
        player.Id,
        time,
        original.RecordDate
    ));
            }

            var orderedPerformances = temporaryPerformances.OrderBy(x => x.Time).ToList();

            for (var i = 0; i < orderedPerformances.Count; i++)
            {
                var performance = orderedPerformances[i];

                snapshot.Performances.Add(
                    new MkddPerformanceEntity
                    {
                        PlayerId = performance.PlayerId,
                        Rank = i + 1,
                        Time = performance.Time,
                        RecordDate = performance.RecordDate
                    });
            }

            _db.Snapshots.Add(snapshot);
        }

        await _db.SaveChangesAsync();
    }
}