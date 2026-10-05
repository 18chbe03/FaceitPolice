using System.Globalization;
using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class MapStatsService
{
    public IReadOnlyList<MapStatistics> Calculate(
        IEnumerable<FaceitStatsResponse> playerStats)
    {
        var matches =
            playerStats
                .SelectMany(x => x.Items)
                .Select(x => x.Stats)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Map))
                .ToList();

        if (matches.Count == 0)
        {
            return [];
        }

        return matches
            .GroupBy(
                x => NormalizeMapName(x.Map),
                StringComparer.OrdinalIgnoreCase)
            .Select(CalculateMap)
            .OrderByDescending(
                x => x.PlayerAppearances)
            .ThenBy(
                x => x.Map)
            .ToList();
    }

    private static MapStatistics CalculateMap(
        IGrouping<string, FaceitMatchStats> group)
    {
        var rows =
            group.ToList();

        var appearances =
            rows.Count;

        var uniqueMatches =
            rows
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.MatchId))
                .Select(x => x.MatchId)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Count();

        var wins =
            rows.Count(
                x => x.Result == "1");

        var losses =
            appearances - wins;

        var kills =
            rows.Sum(
                x => ToInt(x.Kills));

        var deaths =
            rows.Sum(
                x => ToInt(x.Deaths));

        var assists =
            rows.Sum(
                x => ToInt(x.Assists));

        var headshots =
            rows.Sum(
                x => ToInt(x.Headshots));

        var mvps =
            rows.Sum(
                x => ToInt(x.Mvps));

        var rounds =
            rows.Sum(
                x => ToInt(x.Rounds));

        var damage =
            rows.Sum(
                x => ToLong(x.Damage));

        var winRate =
            appearances == 0
                ? 0
                : (double)wins /
                  appearances * 100;

        var kd =
            deaths == 0
                ? kills
                : (double)kills /
                  deaths;

        var kr =
            rounds == 0
                ? 0
                : (double)kills /
                  rounds;

        var averageKills =
            appearances == 0
                ? 0
                : (double)kills /
                  appearances;

        // Mer korrekt än att bara ta snittet
        // av varje match-ADR.
        var adr =
            rounds > 0 && damage > 0
                ? (double)damage / rounds
                : rows.Average(
                    x => ToDouble(x.Adr));

        var headshotPercentage =
            kills == 0
                ? 0
                : (double)headshots /
                  kills * 100;

        var mvpsPerMatch =
            appearances == 0
                ? 0
                : (double)mvps /
                  appearances;

        return new MapStatistics
        {
            Map =
                group.Key,

            UniqueMatches =
                uniqueMatches,

            PlayerAppearances =
                appearances,

            Wins =
                wins,

            Losses =
                losses,

            Kills =
                kills,

            Deaths =
                deaths,

            Assists =
                assists,

            Headshots =
                headshots,

            Mvps =
                mvps,

            Rounds =
                rounds,

            Damage =
                damage,

            WinRate =
                winRate,

            Kd =
                kd,

            Kr =
                kr,

            AverageKills =
                averageKills,

            Adr =
                adr,

            HeadshotPercentage =
                headshotPercentage,

            MvpsPerMatch =
                mvpsPerMatch
        };
    }

    private static string NormalizeMapName(
        string map)
    {
        var value =
            map.Trim();

        if (value.StartsWith(
                "de_",
                StringComparison.OrdinalIgnoreCase))
        {
            value =
                value[3..];
        }

        value =
            value.Replace(
                '_',
                ' ');

        return CultureInfo.InvariantCulture
            .TextInfo
            .ToTitleCase(
                value.ToLowerInvariant());
    }

    private static int ToInt(
        string? value)
    {
        return int.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : 0;
    }

    private static long ToLong(
        string? value)
    {
        return long.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : 0;
    }

    private static double ToDouble(
        string? value)
    {
        return double.TryParse(
            value,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : 0;
    }
}