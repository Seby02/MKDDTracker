using MKDDTracker.Scraper.Data.Database;
using MKDDTracker.Scraper.Parsing;

namespace MKDDTracker.Scraper.Services.SnapshotService;

public sealed class MkddSnapshotImportService
{
    private readonly MkddDbContext _db;

    public MkddSnapshotImportService(MkddDbContext db)
    {
        _db = db;
    }

    public async Task<int> ImportAsync(
        string courseName,
        string html,
        DateOnly rankingDate)
    {
        if (string.IsNullOrWhiteSpace(courseName))
            throw new ArgumentException(
                "Le nom du circuit est obligatoire.");

        if (string.IsNullOrWhiteSpace(html))
            throw new ArgumentException(
                "Le fichier HTML est vide.");

        var parser = new MkddCoursePageParser();

        var performances = parser.Parse(html);

        if (performances.Count == 0)
        {
            throw new InvalidOperationException(
                "Aucune performance trouvée dans le HTML.");
        }

        var snapshotService = new MkddSnapshotService(_db);

        await snapshotService.SaveSnapshotAsync(
            courseName.Trim(),
            performances,
            rankingDate,
            DateTime.UtcNow);

        return performances.Count;
    }
}