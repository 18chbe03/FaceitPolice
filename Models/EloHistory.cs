using System.Text.Json.Serialization;

namespace FaceitPolice.Models;

public sealed class EloHistory
{
    [JsonPropertyName("players")]
    public Dictionary<string, List<EloSnapshot>> Players { get; set; } = [];
}

public sealed class EloSnapshot
{
    [JsonPropertyName("date")]
    public DateOnly Date { get; set; }

    [JsonPropertyName("elo")]
    public int Elo { get; set; }
}