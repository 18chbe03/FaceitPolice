using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class FaceitClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public FaceitClient(
        HttpClient httpClient,
        string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<FaceitPlayer?> GetPlayerAsync(
        string nickname,
        CancellationToken cancellationToken = default)
    {
        var url =
            "https://open.faceit.com/data/v4/players" +
            $"?nickname={Uri.EscapeDataString(nickname)}" +
            "&game=cs2";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content
                .ReadFromJsonAsync<FaceitPlayer>(
                    cancellationToken: cancellationToken);
        }

        if ((int)response.StatusCode != 404)
        {
            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new Exception(
                $"FACEIT-fel för {nickname}: " +
                $"{(int)response.StatusCode} {body}");
        }

        Console.WriteLine(
            $"Exakt sökning misslyckades för {nickname}, söker efter spelaren...");

        return await SearchPlayerAsync(
            nickname,
            cancellationToken);
    }

    private async Task<FaceitPlayer?> SearchPlayerAsync(
        string nickname,
        CancellationToken cancellationToken)
    {
        var url =
            "https://open.faceit.com/data/v4/search/players" +
            $"?nickname={Uri.EscapeDataString(nickname)}" +
            "&game=cs2" +
            "&limit=10";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new Exception(
                $"FACEIT-sökningen misslyckades för {nickname}: " +
                $"{(int)response.StatusCode} {body}");
        }

        var result =
            await response.Content
                .ReadFromJsonAsync<FaceitPlayerSearchResponse>(
                    cancellationToken: cancellationToken);

        var match =
            result?.Items
                .FirstOrDefault(x =>
                    string.Equals(
                        x.Nickname,
                        nickname,
                        StringComparison.OrdinalIgnoreCase));

        match ??=
            result?.Items.FirstOrDefault();

        if (match is null)
        {
            Console.WriteLine(
                $"Ingen FACEIT-spelare hittades för {nickname}");

            return null;
        }

        Console.WriteLine(
            $"Använder FACEIT-spelaren {match.Nickname} för {nickname}");

        return await GetPlayerByIdAsync(
            match.PlayerId,
            cancellationToken);
    }

    private async Task<FaceitPlayer?> GetPlayerByIdAsync(
        string playerId,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://open.faceit.com/data/v4/players/{playerId}";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<FaceitPlayer>(
                cancellationToken: cancellationToken);
    }

    public async Task<FaceitStatsResponse> GetRecentStatsAsync(
        string playerId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var url =
            $"https://open.faceit.com/data/v4/players/" +
            $"{playerId}/games/cs2/stats?limit={limit}";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            throw new Exception(
                $"Kunde inte hämta FACEIT-statistik för {playerId}: " +
                $"{(int)response.StatusCode} {body}");
        }

        var json =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        return JsonSerializer.Deserialize<FaceitStatsResponse>(
                   json,
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   })
               ?? new FaceitStatsResponse();
    }

    public async Task<int> GetMatchCountAsync(
        string playerId,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var now =
            DateTimeOffset.UtcNow;

        var from =
            now
                .AddDays(-days)
                .ToUnixTimeSeconds();

        var to =
            now.ToUnixTimeSeconds();

        const int pageSize = 100;

        var offset = 0;
        var totalMatches = 0;

        while (true)
        {
            var url =
                $"https://open.faceit.com/data/v4/players/{playerId}/history" +
                "?game=cs2" +
                $"&from={from}" +
                $"&to={to}" +
                $"&offset={offset}" +
                $"&limit={pageSize}";

            using var request =
                CreateRequest(url);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body =
                    await response.Content.ReadAsStringAsync(
                        cancellationToken);

                throw new Exception(
                    $"Kunde inte hämta FACEIT-matchhistorik för {playerId}: " +
                    $"{(int)response.StatusCode} {body}");
            }

            var json =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            using var document =
                JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "items",
                    out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var count =
                items.GetArrayLength();

            totalMatches += count;

            if (count < pageSize)
            {
                break;
            }

            offset += pageSize;

            if (offset > 1000)
            {
                break;
            }
        }

        return totalMatches;
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
}