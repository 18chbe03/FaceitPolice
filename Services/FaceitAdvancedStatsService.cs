using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class FaceitAdvancedStatsService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public FaceitAdvancedStatsService(
        HttpClient httpClient,
        string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<IReadOnlyDictionary<string, FaceitAdvancedPlayerStats>> CalculateAsync(
        IReadOnlyList<FaceitAdvancedStatsPlayerInput> players,
        bool includeOpponentElo = false,
        CancellationToken cancellationToken = default)
    {
        var results =
            players.ToDictionary(
                x => x.PlayerId,
                x => new FaceitAdvancedPlayerStats
                {
                    PlayerId = x.PlayerId,
                    Name = x.Name,
                    RequestedMatches = x.MatchIds.Count
                },
                StringComparer.OrdinalIgnoreCase);

        var opponentEloCache =
            new Dictionary<string, int?>(
                StringComparer.OrdinalIgnoreCase);

        var matchToPlayers =
            new Dictionary<string, HashSet<string>>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var player in players)
        {
            foreach (var matchId in player.MatchIds
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!matchToPlayers.TryGetValue(matchId, out var playerIds))
                {
                    playerIds =
                        new HashSet<string>(
                            StringComparer.OrdinalIgnoreCase);

                    matchToPlayers[matchId] =
                        playerIds;
                }

                playerIds.Add(player.PlayerId);
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"🧠 FACEIT Advanced: {players.Count} spelare, " +
            $"{matchToPlayers.Count} unika gruppmatcher att analysera.");

        var successfulMatches = 0;

        foreach (var match in matchToPlayers)
        {
            var matchStats =
                await GetMatchStatsAsync(
                    match.Key,
                    cancellationToken);

            if (matchStats is null)
            {
                continue;
            }

            successfulMatches++;

            foreach (var playerId in match.Value)
            {
                if (!results.TryGetValue(playerId, out var result))
                {
                    continue;
                }

                if (!matchStats.PlayerStats.TryGetValue(playerId, out var playerMatchStats))
                {
                    continue;
                }

                result.AnalyzedMatches++;
                result.TotalRounds += playerMatchStats.Rounds;
                result.TotalEntryAttempts += playerMatchStats.EntryAttempts;
                result.TotalEntryWins += playerMatchStats.EntryWins;
                result.TotalFirstKills += playerMatchStats.FirstKills;
                result.TotalAssists += playerMatchStats.Assists;
                result.TotalPistolKills += playerMatchStats.PistolKills;
                result.TotalOneVsOneAttempts += playerMatchStats.OneVsOneAttempts;
                result.TotalOneVsOneWins += playerMatchStats.OneVsOneWins;
                result.TotalOneVsTwoAttempts += playerMatchStats.OneVsTwoAttempts;
                result.TotalOneVsTwoWins += playerMatchStats.OneVsTwoWins;
                result.TotalClutchKills += playerMatchStats.ClutchKills;
                result.TotalEnemiesFlashed += playerMatchStats.EnemiesFlashed;
                result.TotalFlashCount += playerMatchStats.FlashCount;
                result.TotalFlashSuccesses += playerMatchStats.FlashSuccesses;
                result.TotalUtilityCount += playerMatchStats.UtilityCount;
                result.TotalUtilityDamage += playerMatchStats.UtilityDamage;

                if (includeOpponentElo)
                {
                    var opponentAverageElo =
                        await CalculateOpponentAverageEloAsync(
                            matchStats.Teams,
                            playerId,
                            opponentEloCache,
                            cancellationToken);

                    if (opponentAverageElo.HasValue)
                    {
                        result.TotalOpponentMatchAverageElo +=
                            opponentAverageElo.Value;

                        result.OpponentEloMatches++;
                    }
                }
            }
        }

        Console.WriteLine(
            $"🧠 FACEIT Advanced matcher hämtade: " +
            $"{successfulMatches}/{matchToPlayers.Count}.");

        foreach (var result in results.Values
                     .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"🧠 {result.Name}: {result.AnalyzedMatches}/" +
                $"{result.RequestedMatches} gruppmatcher analyserade.");
        }

        return results;
    }

    private async Task<MatchStatsSnapshot?> GetMatchStatsAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://open.faceit.com/data/v4/matches/" +
            $"{Uri.EscapeDataString(matchId)}/stats";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"⚠️ FACEIT advanced match {matchId}: " +
                $"{(int)response.StatusCode} {response.StatusCode}. Fortsätter.");

            return null;
        }

        try
        {
            using var document =
                JsonDocument.Parse(body);

            var result =
                new MatchStatsSnapshot();

            if (!document.RootElement.TryGetProperty(
                    "rounds",
                    out var rounds) ||
                rounds.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var round in rounds.EnumerateArray())
            {
                var roundsPlayed =
                    GetRoundCount(round);

                if (!round.TryGetProperty(
                        "teams",
                        out var teams) ||
                    teams.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                // Laguppställningen är samma genom matchens stats-rundor.
                // Spara den första kompletta uppställningen för motståndar-ELO.
                var captureTeamRosters =
                    result.Teams.Count == 0;

                foreach (var team in teams.EnumerateArray())
                {
                    if (!team.TryGetProperty(
                            "players",
                            out var players) ||
                        players.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    HashSet<string>? roster =
                        captureTeamRosters
                            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            : null;

                    foreach (var player in players.EnumerateArray())
                    {
                        var playerId =
                            GetString(
                                player,
                                "player_id");

                        if (string.IsNullOrWhiteSpace(playerId))
                        {
                            continue;
                        }

                        roster?.Add(playerId);

                        if (!player.TryGetProperty(
                                "player_stats",
                                out var playerStats) ||
                            playerStats.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        if (!result.PlayerStats.TryGetValue(
                                playerId,
                                out var aggregate))
                        {
                            aggregate =
                                new MatchPlayerStats();

                            result.PlayerStats[playerId] =
                                aggregate;
                        }

                        aggregate.Rounds += roundsPlayed;
                        aggregate.EntryAttempts += GetInt(playerStats, "Entry Count");
                        aggregate.EntryWins += GetInt(playerStats, "Entry Wins");
                        aggregate.FirstKills += GetInt(playerStats, "First Kills");
                        aggregate.Assists += GetInt(playerStats, "Assists");
                        aggregate.PistolKills += GetInt(playerStats, "Pistol Kills");
                        aggregate.OneVsOneAttempts += GetInt(playerStats, "1v1Count");
                        aggregate.OneVsOneWins += GetInt(playerStats, "1v1Wins");
                        aggregate.OneVsTwoAttempts += GetInt(playerStats, "1v2Count");
                        aggregate.OneVsTwoWins += GetInt(playerStats, "1v2Wins");
                        aggregate.ClutchKills += GetInt(playerStats, "Clutch Kills");
                        aggregate.EnemiesFlashed += GetInt(playerStats, "Enemies Flashed");
                        aggregate.FlashCount += GetInt(playerStats, "Flash Count");
                        aggregate.FlashSuccesses += GetInt(playerStats, "Flash Successes");
                        aggregate.UtilityCount += GetInt(playerStats, "Utility Count");
                        aggregate.UtilityDamage += GetDouble(playerStats, "Utility Damage");
                    }

                    if (roster is { Count: > 0 })
                    {
                        result.Teams.Add(roster);
                    }
                }
            }

            return result;
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"⚠️ Kunde inte tolka FACEIT advanced match {matchId}: {ex.Message}");

            return null;
        }
    }

    private async Task<double?> CalculateOpponentAverageEloAsync(
        IReadOnlyList<HashSet<string>> teams,
        string playerId,
        Dictionary<string, int?> eloCache,
        CancellationToken cancellationToken)
    {
        var ownTeamIndex =
            -1;

        for (var i = 0; i < teams.Count; i++)
        {
            if (teams[i].Contains(playerId))
            {
                ownTeamIndex = i;
                break;
            }
        }

        if (ownTeamIndex < 0)
        {
            return null;
        }

        var opponentIds =
            teams
                .Where((_, index) => index != ownTeamIndex)
                .SelectMany(x => x)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (opponentIds.Count == 0)
        {
            return null;
        }

        var elos =
            new List<int>();

        foreach (var opponentId in opponentIds)
        {
            if (!eloCache.TryGetValue(opponentId, out var elo))
            {
                elo =
                    await GetCurrentPlayerEloAsync(
                        opponentId,
                        cancellationToken);

                eloCache[opponentId] =
                    elo;
            }

            if (elo.HasValue)
            {
                elos.Add(elo.Value);
            }
        }

        return elos.Count == 0
            ? null
            : elos.Average();
    }

    private async Task<int?> GetCurrentPlayerEloAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://open.faceit.com/data/v4/players/" +
            $"{Uri.EscapeDataString(playerId)}";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"⚠️ Kunde inte hämta motståndar-ELO för {playerId}: " +
                $"{(int)response.StatusCode} {response.StatusCode}.");

            return null;
        }

        try
        {
            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            var player =
                JsonSerializer.Deserialize<FaceitPlayer>(
                    body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (player is null ||
                !player.Games.TryGetValue("cs2", out var cs2))
            {
                return null;
            }

            return cs2.Elo;
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"⚠️ Kunde inte tolka motståndar-ELO för {playerId}: {ex.Message}");

            return null;
        }
    }

    private static int GetRoundCount(
        JsonElement round)
    {
        if (!round.TryGetProperty(
                "round_stats",
                out var roundStats) ||
            roundStats.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return GetInt(
            roundStats,
            "Rounds");
    }

    private static string GetString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return "";
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? "",
            _ => property.ToString()
        };
    }

    private static int GetInt(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(
            property.ToString(),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : 0;
    }

    private static double GetDouble(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return 0;
        }

        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out var number))
        {
            return number;
        }

        return double.TryParse(
            property.ToString(),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var result)
                ? result
                : 0;
    }

    private HttpRequestMessage CreateRequest(
        string url)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _apiKey);

        return request;
    }

    private sealed class MatchStatsSnapshot
    {
        public Dictionary<string, MatchPlayerStats> PlayerStats { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<HashSet<string>> Teams { get; } =
            [];
    }

    private sealed class MatchPlayerStats
    {
        public int Rounds { get; set; }

        public int EntryAttempts { get; set; }

        public int EntryWins { get; set; }

        public int FirstKills { get; set; }

        public int Assists { get; set; }

        public int PistolKills { get; set; }

        public int OneVsOneAttempts { get; set; }

        public int OneVsOneWins { get; set; }

        public int OneVsTwoAttempts { get; set; }

        public int OneVsTwoWins { get; set; }

        public int ClutchKills { get; set; }

        public int EnemiesFlashed { get; set; }

        public int FlashCount { get; set; }

        public int FlashSuccesses { get; set; }

        public int UtilityCount { get; set; }

        public double UtilityDamage { get; set; }
    }
}
