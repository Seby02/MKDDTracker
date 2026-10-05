namespace MKDDTracker.Scraper.Models;

public sealed record MkddPlayerEvolution(
    string PlayerName,
    string CourseName,
    IReadOnlyList<MkddPlayerEvolutionPoint> Points)
{
    public int TotalRankChange
    {
        get
        {
            if (Points.Count < 2)
            {
                return 0;
            }

            return Points[0].Rank - Points[^1].Rank;
        }
    }

    public TimeSpan TotalTimeChange
    {
        get
        {
            if (Points.Count < 2)
            {
                return TimeSpan.Zero;
            }

            return Points[^1].Time - Points[0].Time;
        }
    }
}