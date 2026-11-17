namespace MKDDTracker.Scraper.Models.Evolution;

public sealed record MkddRankMovementAnalysis(
    string PlayerName,
    string CourseName,
    DateOnly PreviousDate,
    DateOnly CurrentDate,
    int PreviousRank,
    int CurrentRank,
    TimeSpan PreviousTime,
    TimeSpan CurrentTime,
    int CounterfactualRank,
    int TotalPlacesGained,
    int PlacesGainedFromOwnImprovement,
    int PlacesGainedFromOthers,
    IReadOnlyList<MkddPlayerMovement> PlayersWhoMoved,
    IReadOnlyList<MkddRankImpact> RankImpacts)
{
    public bool HasPersonalImprovement => CurrentTime < PreviousTime;

    public int RankDelta => PreviousRank - CurrentRank;

    public TimeSpan TimeDelta => CurrentTime - PreviousTime;

    /// <summary>
    /// Explicit alias used by reports: every item is a crossing, tie transition,
    /// entry, or removal relative to the target player.
    /// </summary>
    public IReadOnlyList<MkddPlayerMovement> DirectMovements => PlayersWhoMoved;
}
