using MKDDTracker.Scraper.Models.Evolution;

namespace MKDDTracker.Scraper.Services.EvolutionService;

public sealed class MkddEvolutionConsoleRenderer
{
    public void Render(MkddPlayerEvolutionReport report)
    {
        Console.WriteLine();
        Console.WriteLine($"{report.PlayerName} - {report.CourseName}");
        Console.WriteLine(new string('-', 48));

        foreach (var step in report.Steps)
        {
            var rankChange =
                step.PreviousRank - step.CurrentRank;

            var timeChange =
                step.CurrentTime - step.PreviousTime;

            Console.WriteLine(
                $"{step.PreviousDate:dd/MM/yyyy} -> " +
                $"{step.CurrentDate:dd/MM/yyyy}");

            Console.WriteLine(
                $"Classement : #{step.PreviousRank} -> " +
                $"#{step.CurrentRank}   " +
                $"{FormatRankChange(rankChange)}");

            Console.WriteLine(
                $"Chrono     : {FormatRaceTime(step.PreviousTime)} -> " +
                $"{FormatRaceTime(step.CurrentTime)}   " +
                $"{FormatTimeChange(timeChange)}");

            Console.WriteLine(
                $"  Rang avec son nouveau chrono seul : " +
                $"#{step.CounterfactualRank}");

            Console.WriteLine(
                $"  Effet de son amélioration : " +
                $"{FormatPlaces(step.PlacesGainedFromOwnImprovement)}");

            Console.WriteLine(
                $"  Effet des autres joueurs : " +
                $"{FormatPlaces(step.PlacesGainedFromOthers)}");

            RenderRankImpacts(step);

            Console.WriteLine();
        }

        RenderGlobalSummary(report);
        RenderDirectMovements(report);
    }

    private static void RenderRankImpacts(
        MkddPlayerEvolutionStep step)
    {
        Console.WriteLine();

        Console.WriteLine(
            "  IMPACT DES AUTRES JOUEURS");

        if (step.RankImpacts.Count == 0)
        {
            Console.WriteLine(
                "    aucun changement d'impact");

            return;
        }

        var impacts =
            step.RankImpacts
                .Where(x => x.RankImpact != 0)
                .OrderBy(x => x.RankImpact)
                .ThenBy(x => x.CurrentRank)
                .ToList();

        if (impacts.Count == 0)
        {
            Console.WriteLine(
                "    aucun changement d'impact");

            return;
        }

        foreach (var impact in impacts)
        {
            Console.WriteLine(
                $"    - {impact.PlayerName} " +
                $"#{impact.PreviousRank} -> " +
                $"#{impact.CurrentRank}");

            Console.WriteLine(
                $"      Chrono : " +
                $"{FormatRaceTime(impact.PreviousTime)} -> " +
                $"{FormatRaceTime(impact.CurrentTime)}");

            Console.WriteLine(
                $"      Impact : " +
                $"{FormatPlaces(impact.RankImpact)}");
        }
    }

    private static void RenderGlobalSummary(
        MkddPlayerEvolutionReport report)
    {
        Console.WriteLine("RESUME DE L'EVOLUTION");
        Console.WriteLine("---------------------");

        Console.WriteLine(
            $"Joueur : {report.PlayerName}");

        Console.WriteLine(
            $"Circuit : {report.CourseName}");

        Console.WriteLine();

        Console.WriteLine(
            $"Progression totale : " +
            $"{FormatPlaces(report.TotalPlacesGained)}");

        Console.WriteLine(
            $"Grâce à ses améliorations : " +
            $"{FormatPlaces(report.TotalPlacesGainedFromOwnImprovement)}");

        Console.WriteLine(
            $"Effet des autres joueurs : " +
            $"{FormatPlaces(report.TotalPlacesGainedFromOthers)}");

        Console.WriteLine(
            $"Evolution du chrono : " +
            $"{FormatTimeChange(report.TotalTimeChange)}");

        Console.WriteLine();
    }

    private static void RenderDirectMovements(
        MkddPlayerEvolutionReport report)
    {
        Console.WriteLine(
            "JOUEURS DIRECTEMENT CROISES");

        Console.WriteLine(
            "---------------------------");

        if (report.DirectMovements.Count == 0)
        {
            Console.WriteLine(
                "Aucun joueur directement croise.");

            Console.WriteLine();

            return;
        }

        var grouped =
            report.DirectMovements
                .GroupBy(x => new
                {
                    x.PreviousDate,
                    x.CurrentDate
                })
                .OrderBy(x => x.Key.PreviousDate);

        foreach (var group in grouped)
        {
            Console.WriteLine();

            Console.WriteLine(
                $"{group.Key.PreviousDate:dd/MM/yyyy} -> " +
                $"{group.Key.CurrentDate:dd/MM/yyyy}");

            var passedPlayers =
                group
                    .Where(x =>
                        x.Relation ==
                        MkddMovementRelation.TargetPassedPlayer)
                    .ToList();

            var playersWhoPassed =
                group
                    .Where(x =>
                        x.Relation ==
                        MkddMovementRelation.PlayerPassedTarget)
                    .ToList();

            Console.WriteLine(
                $"  {report.PlayerName} depasse :");

            if (passedPlayers.Count == 0)
            {
                Console.WriteLine(
                    "    aucun joueur");
            }
            else
            {
                foreach (var movement in passedPlayers)
                {
                    Console.WriteLine(
                        $"    - {movement.PlayerName} " +
                        $"#{movement.PreviousRank} -> " +
                        $"#{movement.CurrentRank}");

                    Console.WriteLine(
                        $"      Chrono : " +
                        $"{FormatRaceTime(movement.PreviousTime)} -> " +
                        $"{FormatRaceTime(movement.CurrentTime)}");

                    Console.WriteLine(
                        $"      Evolution : " +
                        $"{FormatTimeChange(movement.TimeChange)}");

                    Console.WriteLine(
                        $"      Cause : " +
                        $"{FormatMovementCause(
                            movement.Cause,
                            report.PlayerName)}");
                }
            }

            Console.WriteLine();

            Console.WriteLine(
                $"  Joueurs qui depassent {report.PlayerName} :");

            if (playersWhoPassed.Count == 0)
            {
                Console.WriteLine(
                    "    aucun joueur");
            }
            else
            {
                foreach (var movement in playersWhoPassed)
                {
                    Console.WriteLine(
                        $"    - {movement.PlayerName} " +
                        $"#{movement.PreviousRank} -> " +
                        $"#{movement.CurrentRank}");

                    Console.WriteLine(
                        $"      Chrono : " +
                        $"{FormatRaceTime(movement.PreviousTime)} -> " +
                        $"{FormatRaceTime(movement.CurrentTime)}");

                    Console.WriteLine(
                        $"      Evolution : " +
                        $"{FormatTimeChange(movement.TimeChange)}");

                    Console.WriteLine(
                        $"      Cause : " +
                        $"{FormatMovementCause(
                            movement.Cause,
                            report.PlayerName)}");
                }
            }
        }

        Console.WriteLine();
    }

    private static string FormatMovementCause(
        MkddMovementCause cause,
        string targetPlayerName)
    {
        return cause switch
        {
            MkddMovementCause.TargetDriven =>
                $"progression de {targetPlayerName}",

            MkddMovementCause.OpponentDriven =>
                "progression de l'adversaire",

            MkddMovementCause.BothImproved =>
                "progression des deux joueurs",

            _ =>
                "inconnue"
        };
    }

    private static string FormatRankChange(int change)
    {
        if (change > 0)
        {
            var word = change == 1
                ? "place"
                : "places";

            return $"+{change} {word}";
        }

        if (change < 0)
        {
            var absolute = Math.Abs(change);

            var word = absolute == 1
                ? "place"
                : "places";

            return $"-{absolute} {word}";
        }

        return "aucune progression";
    }

    private static string FormatPlaces(int places)
    {
        if (places > 0)
        {
            var word = places == 1
                ? "place"
                : "places";

            return $"+{places} {word}";
        }

        if (places < 0)
        {
            var absolute = Math.Abs(places);

            var word = absolute == 1
                ? "place"
                : "places";

            return $"-{absolute} {word}";
        }

        return "0 place";
    }

    private static string FormatRaceTime(TimeSpan time)
    {
        return
            $"{(int)time.TotalMinutes}:" +
            $"{time.Seconds:00}." +
            $"{time.Milliseconds:000}";
    }

    private static string FormatTimeChange(TimeSpan change)
    {
        var sign = change < TimeSpan.Zero
            ? "-"
            : "+";

        var absolute = change.Duration();

        return $"{sign}{absolute.TotalMilliseconds:0} ms";
    }
}