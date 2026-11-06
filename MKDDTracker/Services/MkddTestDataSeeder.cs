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

    private static TimeSpan GetBestTimeSoFar(
        int playerId,
        DateOnly date,
        IReadOnlyDictionary<(int PlayerId, DateOnly Date), TimeSpan> overrides,
        TimeSpan originalTime)
    {
        var bestTime = originalTime;

        foreach (var entry in overrides)
        {
            if (entry.Key.PlayerId != playerId)
                continue;

            if (entry.Key.Date > date)
                continue;

            if (entry.Value < bestTime)
            {
                bestTime = entry.Value;
            }
        }

        return bestTime;
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

        // -------------------------------------------------
        // SCENARIOS DE TEST
        // -------------------------------------------------
        //
        // IMPORTANT :
        // Une valeur ici représente une nouvelle performance
        // réalisée à cette date.
        //
        // GetBestTimeSoFar() conservera automatiquement
        // le meilleur temps historique.
        //
        // Ainsi :
        //
        // 1:14.500 -> 1:14.700
        //
        // deviendra automatiquement :
        //
        // 1:14.500 -> 1:14.500
        //
        // -------------------------------------------------

        var timeOverrides =
            new Dictionary<(int PlayerId, DateOnly Date), TimeSpan>
            {
                // -----------------------------------------
                // MATTILDE
                // -----------------------------------------

                [(mattilde.Id, new DateOnly(2026, 9, 1))] =
                    TimeSpan.FromMilliseconds(74_642),

                [(mattilde.Id, new DateOnly(2026, 9, 8))] =
                    TimeSpan.FromMilliseconds(74_510),

                [(mattilde.Id, new DateOnly(2026, 9, 15))] =
                    TimeSpan.FromMilliseconds(74_401),

                [(mattilde.Id, new DateOnly(2026, 9, 22))] =
                    TimeSpan.FromMilliseconds(74_210),

                // -----------------------------------------
                // ENZO
                // -----------------------------------------
                //
                // Enzo commence devant Mattilde.
                // Il améliore ensuite son record.
                //
                // Son temps du 22/09 reste à 1:14.200
                // malgré l'absence de nouvelle amélioration.
                // -----------------------------------------

                [(enzo.Id, new DateOnly(2026, 9, 1))] =
                    TimeSpan.FromMilliseconds(74_500),

                [(enzo.Id, new DateOnly(2026, 9, 8))] =
                    TimeSpan.FromMilliseconds(74_500),

                [(enzo.Id, new DateOnly(2026, 9, 15))] =
                    TimeSpan.FromMilliseconds(74_200),

                [(enzo.Id, new DateOnly(2026, 9, 22))] =
                    TimeSpan.FromMilliseconds(74_200)
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

            var temporaryPerformances =
                new List<(
                    int PlayerId,
                    TimeSpan Time,
                    DateOnly RecordDate)>();

            foreach (var original in originalPerformances)
            {
                var player = await _db.Players
                    .SingleOrDefaultAsync(x =>
                        x.SitePlayerId == original.SitePlayerId);

                if (player is null)
                    continue;

                var time = GetBestTimeSoFar(
                    player.Id,
                    date,
                    timeOverrides,
                    original.Time);

                temporaryPerformances.Add(
                    (
                        player.Id,
                        time,
                        original.RecordDate
                    ));
            }

            // -------------------------------------------------
            // CALCUL DU CLASSEMENT
            // -------------------------------------------------

            var orderedPerformances =
                temporaryPerformances
                    .OrderBy(x => x.Time)
                    .ThenBy(x => x.PlayerId)
                    .ToList();

            for (var i = 0; i < orderedPerformances.Count; i++)
            {
                var performance =
                    orderedPerformances[i];

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