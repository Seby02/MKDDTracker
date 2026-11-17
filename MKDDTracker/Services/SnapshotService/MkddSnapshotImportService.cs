using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Data.Entities;
using MKDDTracker.Scraper.Models;
using MKDDTracker.Scraper.Parsing;
using MKDDTracker.Scraper.Services.Ranking;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotImportService
{
    private readonly MkddDbContext _db;

    public MkddSnapshotImportService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<SnapshotImportResult> ImportAsync(
        string courseName,
        string html,
        DateOnly rankingDate,
        CancellationToken cancellationToken = default)
    {
        string normalizedCourseName;
        IReadOnlyList<MkddPerformance> performances;

        try
        {
            normalizedCourseName = ValidateCourse(courseName, rankingDate);

            if (string.IsNullOrWhiteSpace(html))
            {
                throw new ArgumentException("Le fichier HTML est vide.");
            }

            performances = new MkddCoursePageParser().Parse(html);
            ValidatePerformances(performances);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException)
        {
            return Failed(ex.Message);
        }

        IDbContextTransaction? transaction = null;

        try
        {
            transaction =
                await _db.Database.BeginTransactionAsync(cancellationToken);

            var course = await _db.Courses
                .SingleOrDefaultAsync(
                    x => x.Name == normalizedCourseName,
                    cancellationToken);

            if (course is not null)
            {
                var existingSnapshot = await _db.Snapshots
                    .SingleOrDefaultAsync(
                        x => x.CourseId == course.Id &&
                             x.RankingDate == rankingDate,
                        cancellationToken);

                if (existingSnapshot is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    return new SnapshotImportResult(
                        SnapshotImportStatus.AlreadyExists,
                        0,
                        existingSnapshot,
                        null);
                }
            }
            else
            {
                course = new MkddCourse
                {
                    Name = normalizedCourseName
                };

                _db.Courses.Add(course);
            }

            var sitePlayerIds = performances
                .Select(x => x.SitePlayerId)
                .ToArray();

            var existingPlayers = await _db.Players
                .Where(x => sitePlayerIds.Contains(x.SitePlayerId))
                .ToListAsync(cancellationToken);

            var playerBySiteId = existingPlayers
                .ToDictionary(x => x.SitePlayerId);

            foreach (var performance in performances)
            {
                if (!playerBySiteId.TryGetValue(
                        performance.SitePlayerId,
                        out var player))
                {
                    player = new MkddPlayer
                    {
                        SitePlayerId = performance.SitePlayerId,
                        Name = performance.PlayerName,
                        Country = performance.Country
                    };

                    playerBySiteId.Add(player.SitePlayerId, player);
                    _db.Players.Add(player);
                }
                else
                {
                    player.Name = performance.PlayerName;
                    player.Country = performance.Country;
                }
            }

            var snapshot = new MkddSnapshot
            {
                Course = course,
                RankingDate = rankingDate,
                CapturedAt = DateTime.UtcNow
            };

            var rankedPerformances = MkddRankingCalculator.CalculateRanks(
                performances,
                x => x.Time);

            foreach (var rankedPerformance in rankedPerformances)
            {
                var performance = rankedPerformance.Item;

                snapshot.Performances.Add(new MkddPerformanceEntity
                {
                    Player = playerBySiteId[performance.SitePlayerId],
                    Rank = rankedPerformance.Rank,
                    Time = performance.Time,
                    Standard = performance.Standard,
                    RecordDate = performance.RecordDate,
                    VideoUrl = performance.VideoUrl
                });
            }

            _db.Snapshots.Add(snapshot);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new SnapshotImportResult(
                SnapshotImportStatus.Created,
                performances.Count,
                snapshot,
                null);
        }
        catch (Exception ex)
        {
            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Keep the original import error when rollback also fails.
                }
            }

            _db.ChangeTracker.Clear();

            return Failed(
                $"L'import a échoué : {GetInnermostMessage(ex)}");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static string ValidateCourse(
        string courseName,
        DateOnly rankingDate)
    {
        if (string.IsNullOrWhiteSpace(courseName))
        {
            throw new ArgumentException(
                "Le nom du circuit est obligatoire.");
        }

        if (rankingDate == default)
        {
            throw new ArgumentException(
                "La date du classement est invalide.");
        }

        return courseName.Trim();
    }

    private static void ValidatePerformances(
        IReadOnlyList<MkddPerformance> performances)
    {
        if (performances.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucune performance trouvée dans le HTML.");
        }

        foreach (var performance in performances)
        {
            if (performance.SitePlayerId <= 0)
            {
                throw new InvalidOperationException(
                    "Chaque performance doit contenir un identifiant joueur valide.");
            }

            if (string.IsNullOrWhiteSpace(performance.PlayerName))
            {
                throw new InvalidOperationException(
                    $"Le joueur {performance.SitePlayerId} n'a pas de nom.");
            }

            if (performance.Time <= TimeSpan.Zero)
            {
                throw new InvalidOperationException(
                    $"Le chrono du joueur {performance.SitePlayerId} est invalide.");
            }

            if (performance.RecordDate == default)
            {
                throw new InvalidOperationException(
                    $"La date du record du joueur {performance.SitePlayerId} est invalide.");
            }
        }

        var duplicatePlayerId = performances
            .GroupBy(x => x.SitePlayerId)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicatePlayerId is not null)
        {
            throw new InvalidOperationException(
                $"Le joueur {duplicatePlayerId.Value} apparaît plusieurs fois dans le classement.");
        }
    }

    private static SnapshotImportResult Failed(string error) =>
        new(
            SnapshotImportStatus.Failed,
            0,
            null,
            error);

    private static string GetInnermostMessage(Exception exception)
    {
        while (exception.InnerException is not null)
        {
            exception = exception.InnerException;
        }

        return exception.Message;
    }
}
