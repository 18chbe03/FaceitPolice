namespace FaceitPolice.Models;

public sealed class FaceitAdvancedStatsPlayerInput
{
    public string PlayerId { get; init; } = "";

    public string Name { get; init; } = "";

    public IReadOnlyList<string> MatchIds { get; init; } = [];
}

public sealed class FaceitAdvancedPlayerStats
{
    public string PlayerId { get; init; } = "";

    public string Name { get; init; } = "";

    public int RequestedMatches { get; init; }

    public int AnalyzedMatches { get; set; }

    public int TotalRounds { get; set; }

    public int TotalEntryAttempts { get; set; }

    public int TotalEntryWins { get; set; }

    public int TotalFirstKills { get; set; }

    public int TotalAssists { get; set; }

    public int TotalPistolKills { get; set; }

    public int TotalOneVsOneAttempts { get; set; }

    public int TotalOneVsOneWins { get; set; }

    public int TotalOneVsTwoAttempts { get; set; }

    public int TotalOneVsTwoWins { get; set; }

    public int TotalClutchKills { get; set; }

    public int TotalEnemiesFlashed { get; set; }

    public int TotalFlashCount { get; set; }

    public int TotalFlashSuccesses { get; set; }

    public int TotalUtilityCount { get; set; }

    public double TotalUtilityDamage { get; set; }

    public double TotalOpponentMatchAverageElo { get; set; }

    public int OpponentEloMatches { get; set; }

    public double? AverageOpponentElo =>
        OpponentEloMatches == 0
            ? null
            : TotalOpponentMatchAverageElo / OpponentEloMatches;

    public double AverageEntryAttemptsPerMatch =>
        AnalyzedMatches == 0
            ? 0
            : (double)TotalEntryAttempts / AnalyzedMatches;

    public double AverageEntryKillsPerMatch =>
        AnalyzedMatches == 0
            ? 0
            : (double)TotalEntryWins / AnalyzedMatches;

    public double AverageFirstKillsPerMatch =>
        AnalyzedMatches == 0
            ? 0
            : (double)TotalFirstKills / AnalyzedMatches;

    public double AverageAssistsPerMatch =>
        AnalyzedMatches == 0
            ? 0
            : (double)TotalAssists / AnalyzedMatches;

    public double AveragePistolKillsPerMatch =>
        AnalyzedMatches == 0
            ? 0
            : (double)TotalPistolKills / AnalyzedMatches;

    public double? OneVsOneWinPercentage =>
        TotalOneVsOneAttempts == 0
            ? null
            : (double)TotalOneVsOneWins / TotalOneVsOneAttempts * 100;

    public double EntrySuccessPercentage =>
        TotalEntryAttempts == 0
            ? 0
            : (double)TotalEntryWins / TotalEntryAttempts * 100;

    public double EntryRatePercentage =>
        TotalRounds == 0
            ? 0
            : (double)TotalEntryAttempts / TotalRounds * 100;

    public int TotalClutchAttempts =>
        TotalOneVsOneAttempts + TotalOneVsTwoAttempts;

    public int TotalClutchWins =>
        TotalOneVsOneWins + TotalOneVsTwoWins;

    public double? ClutchWinPercentage =>
        TotalClutchAttempts == 0
            ? null
            : (double)TotalClutchWins / TotalClutchAttempts * 100;

    public double UtilityUsagePerRound =>
        TotalRounds == 0
            ? 0
            : (double)TotalUtilityCount / TotalRounds;

    public double UtilityDamagePerRound =>
        TotalRounds == 0
            ? 0
            : TotalUtilityDamage / TotalRounds;

    public double EnemiesFlashedPerRound =>
        TotalRounds == 0
            ? 0
            : (double)TotalEnemiesFlashed / TotalRounds;

    public double FlashSuccessPercentage =>
        TotalFlashCount == 0
            ? 0
            : (double)TotalFlashSuccesses / TotalFlashCount * 100;
}
