using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MKDDTracker.Scraper.Models
{

    public sealed record MkddPerformance(
    int Rank,
    int SitePlayerId,
    string PlayerName,
    string Country,
    TimeSpan Time,
    string Standard,
    DateOnly RecordDate,
    string? VideoUrl);
}
