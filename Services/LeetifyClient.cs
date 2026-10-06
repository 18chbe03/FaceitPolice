using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class LeetifyClient
{
    private const string BaseUrl =
        "https://api-public.cs-prod.leetify.com";

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

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

    public bool HasApiKey =>
        _apiKey is not null;

    public async Task<LeetifyProfileResponse?> GetProfileAsync(
        string steam64Id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(steam64Id))
        {
            return null;
        }

        var url =
            $"{BaseUrl}/v3/profile" +
            $"?steam64_id={Uri.EscapeDataString(steam64Id)}";

        using var request =
            CreateRequest(url);

        using var response =
            await _httpClient.SendAsync(
                request,
                cancellationToken);

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new LeetifyRateLimitException();
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"⚠️ Leetify-profil {steam64Id}: " +
                $"{(int)response.StatusCode} {response.StatusCode}");

            return null;
        }

        return JsonSerializer.Deserialize<LeetifyProfileResponse>(
            body,
            JsonOptions);
    }

    public async Task<LeetifyMatchDetailsResponse?> GetFaceitMatchAsync(
        string faceitMatchId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(faceitMatchId))
        {
            return null;
        }

        foreach (var candidateId in GetCandidateMatchIds(faceitMatchId))
        {
            var url =
                $"{BaseUrl}/v2/matches/faceit/" +
                Uri.EscapeDataString(candidateId);

            using var request =
                CreateRequest(url);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            var body =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new LeetifyRateLimitException();
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine(
                    $"⚠️ Leetify match {candidateId}: " +
                    $"{(int)response.StatusCode} {response.StatusCode}. " +
                    "Fortsätter.");

                continue;
            }

            var match =
                JsonSerializer.Deserialize<LeetifyMatchDetailsResponse>(
                    body,
                    JsonOptions);

            if (match is not null)
            {
                return match;
            }
        }

        return null;
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

    private static IReadOnlyList<string> GetCandidateMatchIds(
        string matchId)
    {
        var candidates =
            new List<string>
            {
                matchId
            };

        if (matchId.StartsWith(
                "1-",
                StringComparison.OrdinalIgnoreCase) &&
            matchId.Length > 2)
        {
            candidates.Add(
                matchId[2..]);
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class LeetifyRateLimitException : Exception
{
    public LeetifyRateLimitException()
        : base("Leetify rate limit nådd.")
    {
    }
}
