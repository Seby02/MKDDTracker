using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddEvolutionReportService
{
    public MkddPlayerEvolutionReport BuildReport(
        MkddCompletePlayerEvolution evolution)
    {
        if (evolution.Steps.Count == 0)
        {
            throw new InvalidOperationException(
                "Impossible de construire un rapport sans étape d'évolution.");
        }

        var firstStep =
            evolution.Steps[0];

        var lastStep =
            evolution.Steps[^1];

        var directMovements =
            evolution.Steps
                .SelectMany(x => x.Movements)
                .OrderBy(x => x.PreviousDate)
                .ThenBy(x => x.CurrentRank)
                .ToList();

        return new MkddPlayerEvolutionReport(
            evolution.PlayerName,
            evolution.CourseName,
            firstStep.PreviousDate,
            lastStep.CurrentDate,
            evolution.Steps,
            evolution.TotalPlacesGained,
            evolution.TotalPlacesGainedFromOwnImprovement,
            evolution.TotalPlacesGainedFromOthers,
            evolution.TotalTimeChange,
            directMovements);
    }
}