namespace FaceitPolice.Models;

public sealed class MapStatistics
{
    public string Map { get; init; } = "";

    public int UniqueMatches { get; init; }

    public int PlayerAppearances { get; init; }

    public int Wins { get; init; }

    public int Losses { get; init; }

    public int Kills { get; init; }

    public int Deaths { get; init; }

    public int Assists { get; init; }

    public int Headshots { get; init; }

    public int Mvps { get; init; }

    public int Rounds { get; init; }

    public long Damage { get; init; }

    public double WinRate { get; init; }

    public double Kd { get; init; }

    public double Kr { get; init; }

    public double AverageKills { get; init; }

    public double Adr { get; init; }

    public double HeadshotPercentage { get; init; }

    public double MvpsPerMatch { get; init; }
}