using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class FaceitAdvancedStatsService
{
    private const string OpponentEloCacheFile =
        "history/opponent-elo-cache.json";

    private static readonly TimeSpan OpponentEloCacheTtl =
        TimeSpan.FromHours(24);

    private static readonly TimeSpan OpponentEloCacheRetention =
        TimeSpan.FromDays(30);

    // FACEIT började ge 429 när advanced stats + motståndar-ELO hämtades i en burst.
    // Vi håller därför ett lugnt tempo och respekterar Retry-After när det finns.
    private static readonly TimeSpan MinimumRequestSpacing =
        TimeSpan.FromMilliseconds(750);

    private const int MaxRetryAttempts = 3;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    private readonly Dictionary<string, MatchStatsSnapshot?> _matchStatsCache =
        new(StringComparer.OrdinalIgnoreCase);

    // Cache för hela programkörningen. Även misslyckade uppslag sparas här så att
    // samma motståndare inte slår API:t om och om igen under samma körning.
    private readonly Dictionary<string, int?> _opponentEloRunCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, OpponentEloCacheEntry> _opponentEloCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _requestSpacingLock =
        new(1, 1);

    private DateTimeOffset _nextRequestAtUtc =
        DateTimeOffset.MinValue;

    private bool _opponentEloCacheLoaded;
    private bool _opponentEloCacheDirty;

    private readonly JsonSerializerOptions _cacheJsonOptions =
        new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

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
        if (includeOpponentElo)
        {
            await EnsureOpponentEloCacheLoadedAsync(
                cancellationToken);
        }

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

        if (includeOpponentElo)
        {
            await SaveOpponentEloCacheAsync(
                cancellationToken);
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
        if (_matchStatsCache.TryGetValue(
                matchId,
                out var cached))
        {
            return cached;
        }

        var url =
            $"https://open.faceit.com/data/v4/matches/" +
            $"{Uri.EscapeDataString(matchId)}/stats";

        using var response =
            await SendFaceitGetWithRetryAsync(
                url,
                $"advanced match {matchId}",
                cancellationToken);

        if (response is null)
        {
            return null;
        }

        var body =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine(
                $"⚠️ FACEIT advanced match {matchId}: " +
                $"{(int)response.StatusCode} {response.StatusCode}. Fortsätter.");

            // Cacha bara permanenta fel. 429/408/5xx ska kunna provas igen senare
            // i samma körning, t.ex. när månadsprognosen återanvänder servicen.
            if (!IsTransientStatusCode(response.StatusCode))
            {
                _matchStatsCache[matchId] = null;
            }

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
                _matchStatsCache[matchId] = result;
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

            _matchStatsCache[matchId] = result;
            return result;
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"⚠️ Kunde inte tolka FACEIT advanced match {matchId}: {ex.Message}");

            _matchStatsCache[matchId] = null;
            return null;
        }
    }

    private async Task<double?> CalculateOpponentAverageEloAsync(
        IReadOnlyList<HashSet<string>> teams,
        string playerId,
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
            var elo =
                await GetCurrentPlayerEloAsync(
                    opponentId,
                    cancellationToken);

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
        if (_opponentEloRunCache.TryGetValue(
                playerId,
                out var runCachedElo))
        {
            return runCachedElo;
        }

        await EnsureOpponentEloCacheLoadedAsync(
            cancellationToken);

        OpponentEloCacheEntry? staleEntry = null;

        if (_opponentEloCache.TryGetValue(
                playerId,
                out var persistedEntry))
        {
            staleEntry = persistedEntry;

            if (DateTimeOffset.UtcNow - persistedEntry.UpdatedAtUtc <
                OpponentEloCacheTtl)
            {
                _opponentEloRunCache[playerId] =
                    persistedEntry.Elo;

                return persistedEntry.Elo;
            }
        }

        var url =
            $"https://open.faceit.com/data/v4/players/" +
            $"{Uri.EscapeDataString(playerId)}";

        using var response =
            await SendFaceitGetWithRetryAsync(
                url,
                $"motståndar-ELO {playerId}",
                cancellationToken);

        if (response is null ||
            !response.IsSuccessStatusCode)
        {
            if (response is not null)
            {
                Console.WriteLine(
                    $"⚠️ Kunde inte hämta motståndar-ELO för {playerId}: " +
                    $"{(int)response.StatusCode} {response.StatusCode}.");
            }

            // Om en gammal cache finns är den bättre än att tappa ELO helt.
            if (staleEntry is not null)
            {
                Console.WriteLine(
                    $"♻️ Använder cachad motståndar-ELO för {playerId} " +
                    $"({staleEntry.Elo}).");

                _opponentEloRunCache[playerId] =
                    staleEntry.Elo;

                return staleEntry.Elo;
            }

            _opponentEloRunCache[playerId] = null;
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
                _opponentEloRunCache[playerId] = null;
                return null;
            }

            var elo =
                cs2.Elo;

            _opponentEloRunCache[playerId] =
                elo;

            _opponentEloCache[playerId] =
                new OpponentEloCacheEntry
                {
                    Elo = elo,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };

            _opponentEloCacheDirty = true;

            return elo;
        }
        catch (JsonException ex)
        {
            Console.WriteLine(
                $"⚠️ Kunde inte tolka motståndar-ELO för {playerId}: {ex.Message}");

            if (staleEntry is not null)
            {
                _opponentEloRunCache[playerId] =
                    staleEntry.Elo;

                return staleEntry.Elo;
            }

            _opponentEloRunCache[playerId] = null;
            return null;
        }
    }

    private async Task<HttpResponseMessage?> SendFaceitGetWithRetryAsync(
        string url,
        string description,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            await WaitForRequestSlotAsync(
                cancellationToken);

            try
            {
                using var request =
                    CreateRequest(url);

                var response =
                    await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                if (!IsTransientStatusCode(response.StatusCode) ||
                    attempt == MaxRetryAttempts)
                {
                    return response;
                }

                var retryDelay =
                    GetRetryDelay(
                        response,
                        attempt);

                Console.WriteLine(
                    $"⏳ FACEIT {(int)response.StatusCode} för {description}. " +
                    $"Försöker igen {attempt + 1}/{MaxRetryAttempts} om " +
                    $"{retryDelay.TotalSeconds:0.#} s.");

                response.Dispose();

                await Task.Delay(
                    retryDelay,
                    cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetryAttempts)
                {
                    Console.WriteLine(
                        $"⚠️ FACEIT-anrop misslyckades för {description}: {ex.Message}");

                    return null;
                }

                var retryDelay =
                    GetFallbackRetryDelay(
                        attempt);

                Console.WriteLine(
                    $"⏳ FACEIT nätverksfel för {description}. " +
                    $"Försöker igen {attempt + 1}/{MaxRetryAttempts} om " +
                    $"{retryDelay.TotalSeconds:0.#} s.");

                await Task.Delay(
                    retryDelay,
                    cancellationToken);
            }
        }

        return null;
    }

    private async Task WaitForRequestSlotAsync(
        CancellationToken cancellationToken)
    {
        await _requestSpacingLock.WaitAsync(
            cancellationToken);

        try
        {
            var now =
                DateTimeOffset.UtcNow;

            if (_nextRequestAtUtc > now)
            {
                await Task.Delay(
                    _nextRequestAtUtc - now,
                    cancellationToken);
            }

            _nextRequestAtUtc =
                DateTimeOffset.UtcNow + MinimumRequestSpacing;
        }
        finally
        {
            _requestSpacingLock.Release();
        }
    }

    private static bool IsTransientStatusCode(
        HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               statusCode == HttpStatusCode.RequestTimeout ||
               (int)statusCode >= 500;
    }

    private static TimeSpan GetRetryDelay(
        HttpResponseMessage response,
        int attempt)
    {
        var retryAfter =
            response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta &&
            delta > TimeSpan.Zero)
        {
            return ClampRetryDelay(delta);
        }

        if (retryAfter?.Date is { } retryDate)
        {
            var untilRetry =
                retryDate - DateTimeOffset.UtcNow;

            if (untilRetry > TimeSpan.Zero)
            {
                return ClampRetryDelay(untilRetry);
            }
        }

        return GetFallbackRetryDelay(attempt);
    }

    private static TimeSpan GetFallbackRetryDelay(
        int attempt)
    {
        var seconds =
            Math.Pow(
                2,
                attempt + 1);

        return TimeSpan.FromSeconds(
            Math.Min(seconds, 30));
    }

    private static TimeSpan ClampRetryDelay(
        TimeSpan delay)
    {
        if (delay < TimeSpan.FromSeconds(1))
        {
            return TimeSpan.FromSeconds(1);
        }

        return delay > TimeSpan.FromMinutes(2)
            ? TimeSpan.FromMinutes(2)
            : delay;
    }

    private async Task EnsureOpponentEloCacheLoadedAsync(
        CancellationToken cancellationToken)
    {
        if (_opponentEloCacheLoaded)
        {
            return;
        }

        _opponentEloCacheLoaded = true;

        if (!File.Exists(OpponentEloCacheFile))
        {
            return;
        }

        try
        {
            var json =
                await File.ReadAllTextAsync(
                    OpponentEloCacheFile,
                    cancellationToken);

            var document =
                JsonSerializer.Deserialize<OpponentEloCacheDocument>(
                    json,
                    _cacheJsonOptions);

            if (document?.Players is null)
            {
                return;
            }

            foreach (var item in document.Players)
            {
                if (!string.IsNullOrWhiteSpace(item.Key) &&
                    item.Value is not null)
                {
                    _opponentEloCache[item.Key] =
                        item.Value;
                }
            }

            Console.WriteLine(
                $"♻️ Motståndar-ELO cache: {_opponentEloCache.Count} spelare laddade.");
        }
        catch (Exception ex) when (
            ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.WriteLine(
                $"⚠️ Kunde inte läsa motståndar-ELO-cachen: {ex.Message}");
        }
    }

    private async Task SaveOpponentEloCacheAsync(
        CancellationToken cancellationToken)
    {
        if (!_opponentEloCacheLoaded ||
            !_opponentEloCacheDirty)
        {
            return;
        }

        var cutoff =
            DateTimeOffset.UtcNow - OpponentEloCacheRetention;

        var expiredIds =
            _opponentEloCache
                .Where(x => x.Value.UpdatedAtUtc < cutoff)
                .Select(x => x.Key)
                .ToList();

        foreach (var playerId in expiredIds)
        {
            _opponentEloCache.Remove(playerId);
        }

        Directory.CreateDirectory("history");

        var document =
            new OpponentEloCacheDocument
            {
                Players =
                    new Dictionary<string, OpponentEloCacheEntry>(
                        _opponentEloCache,
                        StringComparer.OrdinalIgnoreCase)
            };

        var json =
            JsonSerializer.Serialize(
                document,
                _cacheJsonOptions);

        await File.WriteAllTextAsync(
            OpponentEloCacheFile,
            json,
            cancellationToken);

        _opponentEloCacheDirty = false;

        Console.WriteLine(
            $"💾 Motståndar-ELO cache sparad: {_opponentEloCache.Count} spelare.");
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

    private sealed class OpponentEloCacheDocument
    {
        public OpponentEloCacheDocument()
        {
        }

        public Dictionary<string, OpponentEloCacheEntry> Players { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class OpponentEloCacheEntry
    {
        public OpponentEloCacheEntry()
        {
        }

        public int Elo { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
