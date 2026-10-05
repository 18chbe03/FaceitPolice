using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class GroupMatchFilterService
{
    public IReadOnlySet<string> FindQualifiedMatchTeams(
        IEnumerable<FaceitStatsResponse> playerStats,
        int minimumGroupPlayers)
    {
        if (minimumGroupPlayers < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumGroupPlayers),
                "Minst en gruppspelare krävs.");
        }

        var rows =
            playerStats
                .SelectMany(x => x.Items)
                .Select(x => x.Stats)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.MatchId) &&
                    !string.IsNullOrWhiteSpace(x.Team) &&
                    !string.IsNullOrWhiteSpace(x.Nickname))
                .ToList();

        return rows
            .GroupBy(
                x => CreateKey(x.MatchId, x.Team),
                StringComparer.OrdinalIgnoreCase)
            .Where(group =>
                group
                    .Select(x => x.Nickname)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count() >= minimumGroupPlayers)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public FaceitStatsResponse Filter(
        FaceitStatsResponse response,
        IReadOnlySet<string> qualifiedMatchTeams)
    {
        return new FaceitStatsResponse
        {
            Items =
                response.Items
                    .Where(item =>
                    {
                        var stats = item.Stats;

                        if (string.IsNullOrWhiteSpace(stats.MatchId) ||
                            string.IsNullOrWhiteSpace(stats.Team))
                        {
                            return false;
                        }

                        return qualifiedMatchTeams.Contains(
                            CreateKey(stats.MatchId, stats.Team));
                    })
                    .ToList()
        };
    }

    private static string CreateKey(
        string matchId,
        string team)
    {
        return $"{matchId}|{team}";
    }
}
