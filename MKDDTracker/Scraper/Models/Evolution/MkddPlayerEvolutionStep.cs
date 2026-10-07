using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKDDTracker.Scraper.Models.Evolution;

public sealed record MkddPlayerEvolutionStep(
    DateOnly PreviousDate,
    DateOnly CurrentDate,

    int PreviousRank,
    int CurrentRank,

    TimeSpan PreviousTime,
    TimeSpan CurrentTime,

    int TotalPlacesGained,
    int PlacesGainedFromOwnImprovement,
    int PlacesGainedFromOthers,

    IReadOnlyList<MkddPlayerMovement> Movements);
