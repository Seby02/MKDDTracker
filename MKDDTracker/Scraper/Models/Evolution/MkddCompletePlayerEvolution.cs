namespace MKDDTracker.Scraper.Models.Evolution;

public sealed record MkddCompletePlayerEvolution(
    string PlayerName,
    string CourseName,
    IReadOnlyList<MkddPlayerEvolutionStep> Steps)
{
    public int TotalPlacesGained
    {
        get
        {
            if (Steps.Count == 0)
                return 0;

            return Steps[0].PreviousRank -
                   Steps[^1].CurrentRank;
        }
    }

    public TimeSpan TotalTimeChange
    {
        get
        {
            if (Steps.Count == 0)
                return TimeSpan.Zero;

            return Steps[^1].CurrentTime -
                   Steps[0].PreviousTime;
        }
    }

    public int TotalPlacesGainedFromOwnImprovement =>
        Steps.Sum(x => x.PlacesGainedFromOwnImprovement);

    public int TotalPlacesGainedFromOthers =>
        Steps.Sum(x => x.PlacesGainedFromOthers);
}