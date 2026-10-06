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

                if (!matchStats.TryGetValue(playerId, out var playerMatchStats))
                {
                    continue;
                }

                result.AnalyzedMatches++;
                result.TotalRounds += playerMatchStats.Rounds;
                result.TotalEntryAttempts += playerMatchStats.EntryAttempts;
                result.TotalEntryWins += playerMatchStats.EntryWins;
                result.TotalFirstKills += playerMatchStats.FirstKills;
                result.TotalOneVsOneAttempts += playerMatchStats.OneVsOneAttempts;
                result.TotalOneVsOneWins += playerMatchStats.OneVsOneWins;
                result.TotalOneVsTwoAttempts += playerMatchStats.OneVsTwoAttempts;
                result.TotalOneVsTwoWins += playerMatchStats.OneVsTwoWins;
                result.TotalClutchKills += playerMatchStats.ClutchKills;
                result.TotalEnemiesFlashed += playerMatchStats.EnemiesFlashed;
                result.TotalFlashCount += playerMatchStats.FlashCount;
                result.TotalFlashSuccesses += playerMatchStats.FlashSuccesses;
                result.TotalUtilityDamage += playerMatchStats.UtilityDamage;
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

    private async Task<Dictionary<string, MatchPlayerStats>?> GetMatchStatsAsync(
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
                new Dictionary<string, MatchPlayerStats>(
                    StringComparer.OrdinalIgnoreCase);

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

                foreach (var team in teams.EnumerateArray())
                {
                    if (!team.TryGetProperty(
                            "players",
                            out var players) ||
                        players.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var player in players.EnumerateArray())
                    {
                        var playerId =
                            GetString(
                                player,
                                "player_id");

                        if (string.IsNullOrWhiteSpace(playerId) ||
                            !player.TryGetProperty(
                                "player_stats",
                                out var playerStats) ||
                            playerStats.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        if (!result.TryGetValue(
                                playerId,
                                out var aggregate))
                        {
                            aggregate =
                                new MatchPlayerStats();

                            result[playerId] =
                                aggregate;
                        }

                        aggregate.Rounds += roundsPlayed;
                        aggregate.EntryAttempts += GetInt(playerStats, "Entry Count");
                        aggregate.EntryWins += GetInt(playerStats, "Entry Wins");
                        aggregate.FirstKills += GetInt(playerStats, "First Kills");
                        aggregate.OneVsOneAttempts += GetInt(playerStats, "1v1Count");
                        aggregate.OneVsOneWins += GetInt(playerStats, "1v1Wins");
                        aggregate.OneVsTwoAttempts += GetInt(playerStats, "1v2Count");
                        aggregate.OneVsTwoWins += GetInt(playerStats, "1v2Wins");
                        aggregate.ClutchKills += GetInt(playerStats, "Clutch Kills");
                        aggregate.EnemiesFlashed += GetInt(playerStats, "Enemies Flashed");
                        aggregate.FlashCount += GetInt(playerStats, "Flash Count");
                        aggregate.FlashSuccesses += GetInt(playerStats, "Flash Successes");
                        aggregate.UtilityDamage += GetDouble(playerStats, "Utility Damage");
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

    private sealed class MatchPlayerStats
    {
        public int Rounds { get; set; }

        public int EntryAttempts { get; set; }

        public int EntryWins { get; set; }

        public int FirstKills { get; set; }

        public int OneVsOneAttempts { get; set; }

        public int OneVsOneWins { get; set; }

        public int OneVsTwoAttempts { get; set; }

        public int OneVsTwoWins { get; set; }

        public int ClutchKills { get; set; }

        public int EnemiesFlashed { get; set; }

        public int FlashCount { get; set; }

        public int FlashSuccesses { get; set; }

        public double UtilityDamage { get; set; }
    }
}
