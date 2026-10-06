using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FaceitPolice.Services;

public sealed class LeetifyClient
{
    private const string BaseUrl =
        "https://api-public.cs-prod.leetify.com";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public LeetifyClient(
        HttpClient httpClient,
        string? apiKey = null)
    {
        _httpClient = httpClient;
        _apiKey = string.IsNullOrWhiteSpace(apiKey)
            ? null
            : apiKey.Trim();
    }

    public async Task<bool> TryLogFirstAvailableFaceitMatchAsync(
        IEnumerable<string> faceitMatchIds,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts),
                "Minst ett Leetify-försök krävs.");
        }

        var matchIds =
            faceitMatchIds
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(maxAttempts)
                .ToList();

        Console.WriteLine();
        Console.WriteLine(
            "==============================================");
        Console.WriteLine(
            "🧪 LEETIFY - TEST AV FACEIT-MATCH");
        Console.WriteLine(
            "==============================================");

        if (matchIds.Count == 0)
        {
            Console.WriteLine(
                "⚠️ Inga gruppmatcher fanns att testa mot Leetify.");
            Console.WriteLine(
                "==============================================");
            Console.WriteLine();

            return false;
        }

        Console.WriteLine(
            _apiKey is null
                ? "🔓 Kör utan LEETIFY_API_KEY."
                : "🔐 Kör med LEETIFY_API_KEY.");

        Console.WriteLine(
            $"Testar upp till {matchIds.Count} av de senaste gruppmatcherna.");
        Console.WriteLine();

        foreach (var matchId in matchIds)
        {
            var url =
                $"{BaseUrl}/v2/matches/faceit/" +
                Uri.EscapeDataString(matchId);

            using var request =
                CreateRequest(url);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            var json =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            Console.WriteLine(
                $"FACEIT Match ID: {matchId}");

            Console.WriteLine(
                $"Leetify HTTP: {(int)response.StatusCode} " +
                $"{response.StatusCode}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine(
                    "↪️ Matchen finns inte hos Leetify. Testar nästa.");
                Console.WriteLine();
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    "⚠️ Leetify-anropet misslyckades.");

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine(
                        "Rate limit träffad. Lägg till LEETIFY_API_KEY för högre gränser.");
                }

                if (!string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine(
                        $"Svar: {Truncate(json, 1000)}");
                }

                Console.WriteLine(
                    "==============================================");
                Console.WriteLine();

                return false;
            }

            LogMatchFields(
                matchId,
                json);

            Console.WriteLine(
                "==============================================");
            Console.WriteLine();

            return true;
        }

        Console.WriteLine(
            "⚠️ Ingen av de testade gruppmatcherna hittades hos Leetify.");
        Console.WriteLine(
            "==============================================");
        Console.WriteLine();

        return false;
    }

    private HttpRequestMessage CreateRequest(
        string url)
    {
        var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        if (_apiKey is not null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _apiKey);
        }

        return request;
    }

    private static void LogMatchFields(
        string faceitMatchId,
        string json)
    {
        try
        {
            using var document =
                JsonDocument.Parse(json);

            var root =
                document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                Console.WriteLine(
                    "⚠️ Leetify-svaret var inte ett JSON-objekt.");
                Console.WriteLine(
                    $"Svar: {Truncate(json, 2000)}");
                return;
            }

            Console.WriteLine();
            Console.WriteLine(
                "✅ MATCH HITTAD HOS LEETIFY");
            Console.WriteLine(
                $"FACEIT Match ID: {faceitMatchId}");
            Console.WriteLine();

            Console.WriteLine(
                "LEETIFY MATCHFÄLT:");

            foreach (var property in
                     root.EnumerateObject()
                         .Where(x =>
                             !string.Equals(
                                 x.Name,
                                 "stats",
                                 StringComparison.OrdinalIgnoreCase))
                         .OrderBy(
                             x => x.Name,
                             StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"{property.Name} = {GetDisplayValue(property.Value)}");
            }

            Console.WriteLine();

            if (!root.TryGetProperty(
                    "stats",
                    out var stats) ||
                stats.ValueKind != JsonValueKind.Array)
            {
                Console.WriteLine(
                    "⚠️ Leetify-svaret innehöll ingen stats-array.");
                return;
            }

            var statFields =
                new SortedDictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);

            var playerCount = 0;

            foreach (var playerStats in stats.EnumerateArray())
            {
                if (playerStats.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                playerCount++;

                foreach (var property in playerStats.EnumerateObject())
                {
                    if (statFields.ContainsKey(property.Name))
                    {
                        continue;
                    }

                    statFields[property.Name] =
                        GetDisplayValue(property.Value);
                }
            }

            Console.WriteLine(
                $"LEETIFY PLAYER STATS - {playerCount} spelare, " +
                $"{statFields.Count} olika fält:");
            Console.WriteLine();

            foreach (var field in statFields)
            {
                Console.WriteLine(
                    $"{field.Key} = {field.Value}");
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"⚠️ Kunde inte tolka Leetify JSON: {ex.Message}");
            Console.WriteLine(
                $"Svar: {Truncate(json, 2000)}");
        }
    }

    private static string GetDisplayValue(
        JsonElement value)
    {
        var displayValue =
            value.ValueKind switch
            {
                JsonValueKind.String =>
                    value.GetString() ?? "",

                JsonValueKind.Number =>
                    value.ToString(),

                JsonValueKind.True =>
                    "true",

                JsonValueKind.False =>
                    "false",

                JsonValueKind.Null =>
                    "null",

                JsonValueKind.Array =>
                    value.GetArrayLength() == 0
                        ? "[]"
                        : Truncate(value.GetRawText(), 300),

                JsonValueKind.Object =>
                    Truncate(value.GetRawText(), 300),

                _ =>
                    value.ToString()
            };

        return displayValue;
    }

    private static string Truncate(
        string value,
        int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength] + "...";
    }
}
