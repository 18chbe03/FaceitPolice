using System.Text.Json.Serialization;

namespace FaceitPolice.Models;

public sealed class AdvancedStatsPlayerInput
{
    public string Name { get; init; } = "";

    public string Steam64Id { get; init; } = "";

    public List<string> FaceitMatchIds { get; init; } = [];
}

public sealed class AdvancedPlayerStats
{
    public string Name { get; init; } = "";

    public string Steam64Id { get; init; } = "";

    public int RequestedGroupMatches { get; init; }

    public int AnalyzedGroupMatches { get; init; }

    public double LeetifyRating { get; init; }

    public double AccuracyEnemySpottedPercentage { get; init; }

    public double ReactionTimeMs { get; init; }

    public double Preaim { get; init; }

    public double CounterStrafingPercentage { get; init; }

    public double TradeKillsSuccessPercentage { get; init; }

    public double TradeOpportunitiesPerRound { get; init; }

    public double FlashbangHitFoeAverageDuration { get; init; }

    public double HeFoesDamageAverage { get; init; }

    public double SurvivalPercentage { get; init; }

    public double UtilityOnDeathAverage { get; init; }

    public bool HasProfile { get; init; }

    public double? ProfileAim { get; init; }

    public double? ProfilePositioning { get; init; }

    public double? ProfileUtility { get; init; }

    public double? ProfileClutch { get; init; }

    public double? ProfileOpening { get; init; }

    public double? CtOpeningDuelSuccessPercentage { get; init; }

    public double? TOpeningDuelSuccessPercentage { get; init; }

    public double? CtOpeningAggressionSuccessRate { get; init; }

    public double? TOpeningAggressionSuccessRate { get; init; }

    public double CowardiceIndex { get; set; }

    public string PlayStyle { get; set; } = "⚖️ BALANSERAD";
}

public sealed class LeetifyProfileResponse
{
    [JsonPropertyName("steam64_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong Steam64Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("privacy_mode")]
    public string PrivacyMode { get; set; } = "";

    [JsonPropertyName("rating")]
    public LeetifyProfileRating? Rating { get; set; }

    [JsonPropertyName("stats")]
    public LeetifyProfileStats? Stats { get; set; }
}

public sealed class LeetifyProfileRating
{
    [JsonPropertyName("aim")]
    public double? Aim { get; set; }

    [JsonPropertyName("positioning")]
    public double? Positioning { get; set; }

    [JsonPropertyName("utility")]
    public double? Utility { get; set; }

    [JsonPropertyName("clutch")]
    public double? Clutch { get; set; }

    [JsonPropertyName("opening")]
    public double? Opening { get; set; }

    [JsonPropertyName("ct_leetify")]
    public double? CtLeetify { get; set; }

    [JsonPropertyName("t_leetify")]
    public double? TLeetify { get; set; }
}

public sealed class LeetifyProfileStats
{
    [JsonPropertyName("ct_opening_aggression_success_rate")]
    public double? CtOpeningAggressionSuccessRate { get; set; }

    [JsonPropertyName("ct_opening_duel_success_percentage")]
    public double? CtOpeningDuelSuccessPercentage { get; set; }

    [JsonPropertyName("t_opening_aggression_success_rate")]
    public double? TOpeningAggressionSuccessRate { get; set; }

    [JsonPropertyName("t_opening_duel_success_percentage")]
    public double? TOpeningDuelSuccessPercentage { get; set; }
}

public sealed class LeetifyMatchDetailsResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("finished_at")]
    public DateTimeOffset FinishedAt { get; set; }

    [JsonPropertyName("data_source")]
    public string DataSource { get; set; } = "";

    [JsonPropertyName("data_source_match_id")]
    public string DataSourceMatchId { get; set; } = "";

    [JsonPropertyName("map_name")]
    public string MapName { get; set; } = "";

    [JsonPropertyName("stats")]
    public List<LeetifyMatchPlayerStats> Stats { get; set; } = [];
}

public sealed class LeetifyMatchPlayerStats
{
    [JsonPropertyName("steam64_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong Steam64Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("leetify_rating")]
    public double LeetifyRating { get; set; }

    [JsonPropertyName("rounds_count")]
    public int RoundsCount { get; set; }

    [JsonPropertyName("shots_fired_enemy_spotted")]
    public int ShotsFiredEnemySpotted { get; set; }

    [JsonPropertyName("shots_hit_enemy_spotted")]
    public int ShotsHitEnemySpotted { get; set; }

    [JsonPropertyName("reaction_time")]
    public double ReactionTime { get; set; }

    [JsonPropertyName("preaim")]
    public double Preaim { get; set; }

    [JsonPropertyName("counter_strafing_shots_all")]
    public int CounterStrafingShotsAll { get; set; }

    [JsonPropertyName("counter_strafing_shots_good")]
    public int CounterStrafingShotsGood { get; set; }

    [JsonPropertyName("flashbang_hit_foe")]
    public int FlashbangHitFoe { get; set; }

    [JsonPropertyName("flashbang_hit_foe_avg_duration")]
    public double FlashbangHitFoeAverageDuration { get; set; }

    [JsonPropertyName("he_thrown")]
    public int HeThrown { get; set; }

    [JsonPropertyName("he_foes_damage_avg")]
    public double HeFoesDamageAverage { get; set; }

    [JsonPropertyName("trade_kills_succeed")]
    public int TradeKillsSucceed { get; set; }

    [JsonPropertyName("trade_kill_attempts")]
    public int TradeKillAttempts { get; set; }

    [JsonPropertyName("trade_kill_opportunities")]
    public int TradeKillOpportunities { get; set; }

    [JsonPropertyName("rounds_survived")]
    public int RoundsSurvived { get; set; }

    [JsonPropertyName("total_deaths")]
    public int TotalDeaths { get; set; }

    [JsonPropertyName("utility_on_death_avg")]
    public double UtilityOnDeathAverage { get; set; }
}
