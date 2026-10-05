using System.Text.Json.Serialization;

namespace FaceitPolice.Models;

public sealed class FaceitPlayer
{
    [JsonPropertyName("player_id")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("games")]
    public Dictionary<string, FaceitGame> Games { get; set; } = [];
}

public sealed class FaceitGame
{
    [JsonPropertyName("faceit_elo")]
    public int Elo { get; set; }

    [JsonPropertyName("skill_level")]
    public int SkillLevel { get; set; }
}

public sealed class FaceitStatsResponse
{
    [JsonPropertyName("items")]
    public List<FaceitStatsItem> Items { get; set; } = [];
}

public sealed class FaceitStatsItem
{
    [JsonPropertyName("stats")]
    public FaceitMatchStats Stats { get; set; } = new();
}

public sealed class FaceitMatchStats
{
    [JsonPropertyName("Nickname")]
    public string Nickname { get; set; } = "";

    [JsonPropertyName("Result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("Kills")]
    public string Kills { get; set; } = "0";

    [JsonPropertyName("Deaths")]
    public string Deaths { get; set; } = "0";

    [JsonPropertyName("Assists")]
    public string Assists { get; set; } = "0";

    [JsonPropertyName("Headshots")]
    public string Headshots { get; set; } = "0";

    [JsonPropertyName("Headshots %")]
    public string HeadshotPercentage { get; set; } = "0";

    [JsonPropertyName("ADR")]
    public string Adr { get; set; } = "0";

    [JsonPropertyName("K/D Ratio")]
    public string KdRatio { get; set; } = "0";

    [JsonPropertyName("Map")]
    public string Map { get; set; } = "";

    [JsonPropertyName("MVPs")]
    public string Mvps { get; set; } = "0";

    [JsonPropertyName("Triple Kills")]
    public string TripleKills { get; set; } = "0";

    [JsonPropertyName("Quadro Kills")]
    public string QuadroKills { get; set; } = "0";

    [JsonPropertyName("Penta Kills")]
    public string PentaKills { get; set; } = "0";

    [JsonPropertyName("Match Finished At")]
    public long MatchFinishedAt { get; set; }
}


public sealed class FaceitPlayerSearchResponse
{
    [JsonPropertyName("items")]
    public List<FaceitPlayerSearchItem> Items { get; set; } = [];
}

public sealed class FaceitPlayerSearchItem
{
    [JsonPropertyName("player_id")]
    public string PlayerId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";
}