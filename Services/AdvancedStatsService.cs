using FaceitPolice.Models;

namespace FaceitPolice.Services;

public sealed class AdvancedStatsService
{
    private readonly LeetifyClient _leetifyClient;

    public AdvancedStatsService(
        LeetifyClient leetifyClient)
    {
        _leetifyClient = leetifyClient;
    }

    public async Task<IReadOnlyList<AdvancedPlayerStats>> CalculateAsync(
        IReadOnlyList<AdvancedStatsPlayerInput> players,
        CancellationToken cancellationToken = default)
    {
        var validPlayers =
            players
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.Name) &&
                    !string.IsNullOrWhiteSpace(x.Steam64Id))
                .ToList();

        if (validPlayers.Count == 0)
        {
            Console.WriteLine(
                "⚠️ Ingen spelare hade Steam64-ID för Leetify Advanced Stats.");

            return [];
        }

        var uniqueMatchIds =
            validPlayers
                .SelectMany(x => x.FaceitMatchIds)
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        Console.WriteLine();
        Console.WriteLine(
            $"🧠 Advanced Stats: {validPlayers.Count} spelare, " +
            $"{uniqueMatchIds.Count} unika gruppmatcher att kontrollera hos Leetify.");

        if (!_leetifyClient.HasApiKey)
        {
            Console.WriteLine(
                "⚠️ LEETIFY_API_KEY saknas. Kör med striktare rate limits.");
        }

        var matches =
            new Dictionary<string, LeetifyMatchDetailsResponse>(
                StringComparer.OrdinalIgnoreCase);

        var rateLimited = false;

        foreach (var matchId in uniqueMatchIds)
        {
            try
            {
                var match =
                    await _leetifyClient.GetFaceitMatchAsync(
                        matchId,
                        cancellationToken);

                if (match is not null)
                {
                    matches[matchId] = match;
                }

                await DelayIfUnauthenticatedAsync(
                    cancellationToken);
            }
            catch (LeetifyRateLimitException)
            {
                Console.WriteLine(
                    "⚠️ Leetify rate limit nådd under matchhämtningen. " +
                    "Bygger tavlan med datan som hann hämtas.");

                rateLimited = true;
                break;
            }
        }

        Console.WriteLine(
            $"🧠 Leetify matcher hämtade: {matches.Count}/{uniqueMatchIds.Count}.");

        var profiles =
            new Dictionary<ulong, LeetifyProfileResponse>();

        if (!rateLimited)
        {
            foreach (var player in validPlayers)
            {
                if (!ulong.TryParse(
                        player.Steam64Id,
                        out var steam64Id))
                {
                    continue;
                }

                try
                {
                    var profile =
                        await _leetifyClient.GetProfileAsync(
                            player.Steam64Id,
                            cancellationToken);

                    if (profile is not null)
                    {
                        profiles[steam64Id] = profile;
                    }

                    await DelayIfUnauthenticatedAsync(
                        cancellationToken);
                }
                catch (LeetifyRateLimitException)
                {
                    Console.WriteLine(
                        "⚠️ Leetify rate limit nådd under profilhämtningen. " +
                        "Fortsätter utan fler profilvärden.");

                    break;
                }
            }
        }

        var results =
            new List<AdvancedPlayerStats>();

        foreach (var player in validPlayers)
        {
            if (!ulong.TryParse(
                    player.Steam64Id,
                    out var steam64Id))
            {
                continue;
            }

            var rows =
                new List<LeetifyMatchPlayerStats>();

            foreach (var matchId in player.FaceitMatchIds)
            {
                if (!matches.TryGetValue(
                        matchId,
                        out var match))
                {
                    continue;
                }

                var row =
                    match.Stats.FirstOrDefault(
                        x => x.Steam64Id == steam64Id);

                if (row is not null)
                {
                    rows.Add(row);
                }
            }

            profiles.TryGetValue(
                steam64Id,
                out var profile);

            results.Add(
                AggregatePlayer(
                    player,
                    rows,
                    profile));
        }

        ApplyCowardiceIndex(
            results);

        foreach (var result in results)
        {
            Console.WriteLine(
                $"🧠 {result.Name}: {result.AnalyzedGroupMatches}/" +
                $"{result.RequestedGroupMatches} gruppmatcher analyserade hos Leetify.");
        }

        return results;
    }

    private async Task DelayIfUnauthenticatedAsync(
        CancellationToken cancellationToken)
    {
        if (!_leetifyClient.HasApiKey)
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(200),
                cancellationToken);
        }
    }

    private static AdvancedPlayerStats AggregatePlayer(
        AdvancedStatsPlayerInput player,
        IReadOnlyList<LeetifyMatchPlayerStats> rows,
        LeetifyProfileResponse? profile)
    {
        var rounds =
            rows.Sum(x => x.RoundsCount);

        var spottedShots =
            rows.Sum(x => x.ShotsFiredEnemySpotted);

        var spottedHits =
            rows.Sum(x => x.ShotsHitEnemySpotted);

        var counterStrafeShots =
            rows.Sum(x => x.CounterStrafingShotsAll);

        var counterStrafeGood =
            rows.Sum(x => x.CounterStrafingShotsGood);

        var tradeAttempts =
            rows.Sum(x => x.TradeKillAttempts);

        var tradeSuccesses =
            rows.Sum(x => x.TradeKillsSucceed);

        var tradeOpportunities =
            rows.Sum(x => x.TradeKillOpportunities);

        var survivedRounds =
            rows.Sum(x => x.RoundsSurvived);

        var deaths =
            rows.Sum(x => x.TotalDeaths);

        return new AdvancedPlayerStats
        {
            Name = player.Name,
            Steam64Id = player.Steam64Id,
            RequestedGroupMatches = player.FaceitMatchIds.Count,
            AnalyzedGroupMatches = rows.Count,

            LeetifyRating =
                WeightedAverage(
                    rows,
                    x => x.LeetifyRating,
                    x => x.RoundsCount),

            AccuracyEnemySpottedPercentage =
                Percentage(
                    spottedHits,
                    spottedShots),

            ReactionTimeMs =
                WeightedAverage(
                    rows.Where(x => x.ReactionTime > 0).ToList(),
                    x => x.ReactionTime * 1000,
                    x => Math.Max(x.ShotsFiredEnemySpotted, 1)),

            Preaim =
                WeightedAverage(
                    rows.Where(x => x.Preaim > 0).ToList(),
                    x => x.Preaim,
                    x => Math.Max(x.ShotsFiredEnemySpotted, 1)),

            CounterStrafingPercentage =
                Percentage(
                    counterStrafeGood,
                    counterStrafeShots),

            TradeKillsSuccessPercentage =
                Percentage(
                    tradeSuccesses,
                    tradeAttempts),

            TradeOpportunitiesPerRound =
                rounds == 0
                    ? 0
                    : (double)tradeOpportunities / rounds,

            FlashbangHitFoeAverageDuration =
                WeightedAverage(
                    rows.Where(x => x.FlashbangHitFoe > 0).ToList(),
                    x => x.FlashbangHitFoeAverageDuration,
                    x => x.FlashbangHitFoe),

            HeFoesDamageAverage =
                WeightedAverage(
                    rows.Where(x => x.HeThrown > 0).ToList(),
                    x => x.HeFoesDamageAverage,
                    x => x.HeThrown),

            SurvivalPercentage =
                Percentage(
                    survivedRounds,
                    rounds),

            UtilityOnDeathAverage =
                WeightedAverage(
                    rows.Where(x => x.TotalDeaths > 0).ToList(),
                    x => x.UtilityOnDeathAverage,
                    x => x.TotalDeaths),

            HasProfile =
                profile is not null,

            ProfileAim =
                profile?.Rating?.Aim,

            ProfilePositioning =
                profile?.Rating?.Positioning,

            ProfileUtility =
                profile?.Rating?.Utility,

            ProfileClutch =
                profile?.Rating?.Clutch,

            ProfileOpening =
                profile?.Rating?.Opening,

            CtOpeningDuelSuccessPercentage =
                profile?.Stats?.CtOpeningDuelSuccessPercentage,

            TOpeningDuelSuccessPercentage =
                profile?.Stats?.TOpeningDuelSuccessPercentage,

            CtOpeningAggressionSuccessRate =
                profile?.Stats?.CtOpeningAggressionSuccessRate,

            TOpeningAggressionSuccessRate =
                profile?.Stats?.TOpeningAggressionSuccessRate
        };
    }

    private static void ApplyCowardiceIndex(
        IReadOnlyList<AdvancedPlayerStats> players)
    {
        var eligible =
            players
                .Where(x =>
                    x.AnalyzedGroupMatches > 0)
                .ToList();

        if (eligible.Count == 0)
        {
            return;
        }

        var minSurvival =
            eligible.Min(x => x.SurvivalPercentage);

        var maxSurvival =
            eligible.Max(x => x.SurvivalPercentage);

        var minTradeOpportunities =
            eligible.Min(x => x.TradeOpportunitiesPerRound);

        var maxTradeOpportunities =
            eligible.Max(x => x.TradeOpportunitiesPerRound);

        var minUtilityOnDeath =
            eligible.Min(x => x.UtilityOnDeathAverage);

        var maxUtilityOnDeath =
            eligible.Max(x => x.UtilityOnDeathAverage);

        foreach (var player in eligible)
        {
            var survivalScore =
                Normalize(
                    player.SurvivalPercentage,
                    minSurvival,
                    maxSurvival);

            var lowTradeScore =
                1 - Normalize(
                    player.TradeOpportunitiesPerRound,
                    minTradeOpportunities,
                    maxTradeOpportunities);

            var utilityOnDeathScore =
                Normalize(
                    player.UtilityOnDeathAverage,
                    minUtilityOnDeath,
                    maxUtilityOnDeath);

            player.CowardiceIndex =
                Math.Round(
                    100 *
                    (
                        0.45 * survivalScore +
                        0.40 * lowTradeScore +
                        0.15 * utilityOnDeathScore
                    ));

            player.PlayStyle =
                player.CowardiceIndex switch
                {
                    <= 33 => "🦍 FRONTLINJEN",
                    >= 67 => "🐔 BAKRADSOPERATÖR",
                    _ => "⚖️ BALANSERAD"
                };
        }
    }

    private static double WeightedAverage(
        IReadOnlyList<LeetifyMatchPlayerStats> rows,
        Func<LeetifyMatchPlayerStats, double> valueSelector,
        Func<LeetifyMatchPlayerStats, int> weightSelector)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        double weightedSum = 0;
        long totalWeight = 0;

        foreach (var row in rows)
        {
            var weight =
                Math.Max(
                    weightSelector(row),
                    0);

            if (weight == 0)
            {
                continue;
            }

            weightedSum +=
                valueSelector(row) * weight;

            totalWeight +=
                weight;
        }

        return totalWeight == 0
            ? 0
            : weightedSum / totalWeight;
    }

    private static double Percentage(
        int numerator,
        int denominator)
    {
        return denominator == 0
            ? 0
            : (double)numerator /
              denominator * 100;
    }

    private static double Normalize(
        double value,
        double min,
        double max)
    {
        if (Math.Abs(max - min) < 0.000001)
        {
            return 0.5;
        }

        return Math.Clamp(
            (value - min) / (max - min),
            0,
            1);
    }
}
