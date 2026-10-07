namespace MKDDTracker.Scraper.Models;

public sealed record MkddRankMovementAnalysis(
    int PreviousRank,
    int CurrentRank,
    int CounterfactualRank,
    int TotalPlacesGained,
    int PlacesGainedFromOwnImprovement,
    int PlacesGainedFromOthers,
    TimeSpan PreviousTime,
    TimeSpan CurrentTime);