using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKDDTracker.Scraper.Models.Evolution
{

    public sealed record MkddPlayerMovement(
        int PlayerId,
        string PlayerName,
        int PreviousRank,
        int CurrentRank,
        TimeSpan PreviousTime,
        TimeSpan CurrentTime,
        int RankChange,
        MkddMovementRelation Relation);
}
