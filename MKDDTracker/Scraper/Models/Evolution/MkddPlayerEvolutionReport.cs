namespace MKDDTracker.Scraper.Models.Evolution;

public sealed record MkddPlayerEvolutionReport(
    string PlayerName,
    string CourseName,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<MkddPlayerEvolutionStep> Steps,
    int TotalPlacesGained,
    int TotalPlacesGainedFromOwnImprovement,
    int TotalPlacesGainedFromOthers,
    TimeSpan TotalTimeChange,
    IReadOnlyList<MkddPlayerMovement> DirectMovements);