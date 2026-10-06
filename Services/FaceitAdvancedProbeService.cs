using System.Net.Http.Headers;
using System.Text.Json;

namespace FaceitPolice.Services;

public sealed class FaceitAdvancedProbeService
{
    private static readonly string[] AdvancedKeywords =
    [
        "open",
        "entry",
        "clutch",
        "trade",
        "utility",
        "flash",
        "grenade",
        "he ",
        "he_",
        "molotov",
        "smoke",
        "rating",
        "rws",
        "impact",
        "assist"
    ];

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public FaceitAdvancedProbeService(
        HttpClient httpClient,
        string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task ProbeAsync(
        string playerId,
        IEnumerable<string> matchIds,
        int maxMatches = 3,
        CancellationToken cancellationToken = default)
    {
        var distinctMatchIds =
            matchIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxMatches)
                .ToList();

        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine("🧪 FACEIT ADVANCED STATS - OFFICIELLT API");
        Console.WriteLine("==============================================");
        Console.WriteLine(
            $"Testar player stats + upp till {distinctMatchIds.Count} gruppmatcher.");
        Console.WriteLine();

        await ProbePlayerStatsAsync(
            playerId,
            cancellationToken);

        foreach (var matchId in distinctMatchIds)
        {
            await ProbeMatchStatsAsync(
                matchId,
                cancellationToken);
        }

        Console.WriteLine("==============================================");
        Console.WriteLine("🧪 FACEIT ADVANCED STATS - TEST KLART");
        Console.WriteLine("==============================================");
        Console.WriteLine();
    }

    private async Task ProbePlayerStatsAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://open.faceit.com/data/v4/players/{playerId}/stats/cs2";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        Console.WriteLine("👤 PLAYER STATS ENDPOINT");
        Console.WriteLine($"HTTP: {(int)response.StatusCode} {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"⚠️ Svar: {Truncate(body, 500)}");
            Console.WriteLine();
            return;
        }

        using var document =
            JsonDocument.Parse(body);

        var fields =
            new SortedDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.TryGetProperty(
                "lifetime",
                out var lifetime))
        {
            CollectLeafFields(
                lifetime,
                "lifetime",
                fields);
        }

        if (document.RootElement.TryGetProperty(
                "segments",
                out var segments) &&
            segments.ValueKind == JsonValueKind.Array)
        {
            var index = 0;

            foreach (var segment in segments.EnumerateArray())
            {
                CollectLeafFields(
                    segment,
                    $"segments[{index}]",
                    fields);

                index++;

                if (index >= 5)
                {
                    break;
                }
            }
        }

        Console.WriteLine(
            $"Fält hittade i lifetime/segments: {fields.Count}");

        PrintCandidateFields(fields);
        Console.WriteLine();
    }

    private async Task ProbeMatchStatsAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://open.faceit.com/data/v4/matches/{Uri.EscapeDataString(matchId)}/stats";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        Console.WriteLine($"🎮 MATCH STATS: {matchId}");
        Console.WriteLine($"HTTP: {(int)response.StatusCode} {response.StatusCode}");

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"⚠️ Svar: {Truncate(body, 500)}");
            Console.WriteLine();
            return;
        }

        using var document =
            JsonDocument.Parse(body);

        var playerFields =
            new SortedDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var teamFields =
            new SortedDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        var roundFields =
            new SortedDictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        if (document.RootElement.TryGetProperty(
                "rounds",
                out var rounds) &&
            rounds.ValueKind == JsonValueKind.Array)
        {
            foreach (var round in rounds.EnumerateArray())
            {
                if (round.TryGetProperty(
                        "round_stats",
                        out var roundStats) &&
                    roundStats.ValueKind == JsonValueKind.Object)
                {
                    CollectObjectProperties(
                        roundStats,
                        roundFields);
                }

                if (!round.TryGetProperty(
                        "teams",
                        out var teams) ||
                    teams.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var team in teams.EnumerateArray())
                {
                    if (team.TryGetProperty(
                            "team_stats",
                            out var teamStats) &&
                        teamStats.ValueKind == JsonValueKind.Object)
                    {
                        CollectObjectProperties(
                            teamStats,
                            teamFields);
                    }

                    if (!team.TryGetProperty(
                            "players",
                            out var players) ||
                        players.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (var player in players.EnumerateArray())
                    {
                        if (player.TryGetProperty(
                                "player_stats",
                                out var playerStats) &&
                            playerStats.ValueKind == JsonValueKind.Object)
                        {
                            CollectObjectProperties(
                                playerStats,
                                playerFields);
                        }
                    }
                }
            }
        }

        Console.WriteLine(
            $"Player stats-fält: {playerFields.Count}");
        Console.WriteLine(
            $"Team stats-fält: {teamFields.Count}");
        Console.WriteLine(
            $"Round stats-fält: {roundFields.Count}");

        Console.WriteLine();
        Console.WriteLine("PLAYER_STATS - ALLA FÄLT:");

        foreach (var field in playerFields)
        {
            Console.WriteLine(
                $"{field.Key} = {field.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("MÖJLIGA ADVANCED-FÄLT:");

        var candidates =
            playerFields
                .Where(x => IsAdvancedField(x.Key))
                .ToList();

        if (candidates.Count == 0)
        {
            Console.WriteLine(
                "❌ Inga tydliga opening/entry/clutch/trade/utility-fält hittades i player_stats.");
        }
        else
        {
            foreach (var field in candidates)
            {
                Console.WriteLine(
                    $"✅ {field.Key} = {field.Value}");
            }
        }

        if (teamFields.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("TEAM_STATS:");

            foreach (var field in teamFields)
            {
                Console.WriteLine(
                    $"{field.Key} = {field.Value}");
            }
        }

        if (roundFields.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("ROUND_STATS:");

            foreach (var field in roundFields)
            {
                Console.WriteLine(
                    $"{field.Key} = {field.Value}");
            }
        }

        Console.WriteLine();
    }

    private static void CollectObjectProperties(
        JsonElement element,
        IDictionary<string, string> fields)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!fields.ContainsKey(property.Name))
            {
                fields[property.Name] =
                    GetDisplayValue(
                        property.Value);
            }
        }
    }

    private static void CollectLeafFields(
        JsonElement element,
        string path,
        IDictionary<string, string> fields)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectLeafFields(
                        property.Value,
                        $"{path}.{property.Name}",
                        fields);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;

                foreach (var item in element.EnumerateArray())
                {
                    CollectLeafFields(
                        item,
                        $"{path}[{index}]",
                        fields);

                    index++;

                    if (index >= 3)
                    {
                        break;
                    }
                }
                break;

            default:
                if (!fields.ContainsKey(path))
                {
                    fields[path] =
                        GetDisplayValue(element);
                }
                break;
        }
    }

    private static void PrintCandidateFields(
        IReadOnlyDictionary<string, string> fields)
    {
        var candidates =
            fields
                .Where(x => IsAdvancedField(x.Key))
                .ToList();

        Console.WriteLine("Möjliga advanced-fält:");

        if (candidates.Count == 0)
        {
            Console.WriteLine(
                "❌ Inga tydliga opening/entry/clutch/trade/utility-fält hittades.");
            return;
        }

        foreach (var field in candidates)
        {
            Console.WriteLine(
                $"✅ {field.Key} = {field.Value}");
        }
    }

    private static bool IsAdvancedField(
        string fieldName)
    {
        return AdvancedKeywords.Any(keyword =>
            fieldName.Contains(
                keyword,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string GetDisplayValue(
        JsonElement value)
    {
        var text =
            value.ValueKind switch
            {
                JsonValueKind.String =>
                    value.GetString() ?? string.Empty,

                JsonValueKind.Number =>
                    value.ToString(),

                JsonValueKind.True =>
                    "true",

                JsonValueKind.False =>
                    "false",

                JsonValueKind.Null =>
                    "null",

                _ =>
                    value.GetRawText()
            };

        return Truncate(
            text,
            200);
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

    private static string Truncate(
        string value,
        int maxLength)
    {
        if (string.IsNullOrEmpty(value) ||
            value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
