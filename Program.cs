using System.Globalization;

using System.Text;

using FaceitPolice.Models;

using FaceitPolice.Services;



// --------------------------------------------------

// INSTÄLLNINGAR

// --------------------------------------------------



const int PlayerStatsMatchLimit = 10;

const int MapStatsMatchLimit = 50;

const int ActivityDays = 30;

const int MinimumGroupPlayersPerMatch = 3;

const int MinimumGroupMatchesForRanking = 2;

const int MinimumGroupMatchesForAwards = 5;





// --------------------------------------------------

// ENVIRONMENT VARIABLES

// --------------------------------------------------



var faceitApiKey =

    Environment.GetEnvironmentVariable(

        "FACEIT_API_KEY")

    ?? throw new InvalidOperationException(

        "FACEIT_API_KEY saknas.");



var discordWebhook =

    Environment.GetEnvironmentVariable(

        "DISCORD_WEBHOOK_URL")

    ?? throw new InvalidOperationException(

        "DISCORD_WEBHOOK_URL saknas.");



var configuredBoardMessageId =

    Environment.GetEnvironmentVariable(

        "DISCORD_MESSAGE_ID");



var configuredTiltWatchMessageId =

    Environment.GetEnvironmentVariable(

        "TILT_WATCH_MESSAGE_ID");



var configuredMapStatsMessageId =

    Environment.GetEnvironmentVariable(

        "MAP_STATS_MESSAGE_ID");



var configuredAwardsMessageId =

    Environment.GetEnvironmentVariable(

        "AWARDS_MESSAGE_ID");



var playersConfig =

    Environment.GetEnvironmentVariable(

        "FACEIT_PLAYERS")

    ?? "chrillebille,BOSSEN-_-,ibbann,roevklo,raveleif,DILLicious,-__SMILE__-,fenUH,Willyo,Frodefrode3,vicyo,hebben8or,-starscream";





// --------------------------------------------------

// SERVICES

// --------------------------------------------------



using var httpClient =

    new HttpClient();



var faceitClient =

    new FaceitClient(

        httpClient,

        faceitApiKey);



var discordClient =

    new DiscordClient(

        httpClient,

        discordWebhook);



var historyService =

    new EloHistoryService();



var mapStatsService =

    new MapStatsService();



var groupMatchFilterService =

    new GroupMatchFilterService();



var faceitAdvancedStatsService =

    new FaceitAdvancedStatsService(

        httpClient,

        faceitApiKey);



var discordStateService =

    new DiscordMessageStateService();





// --------------------------------------------------

// LADDA STATE

// --------------------------------------------------



await historyService.LoadAsync();



var discordState =

    await discordStateService.LoadAsync();





// Env vinner över state.
// State används endast som fallback.

var boardMessageId =
    configuredBoardMessageId
    ?? discordState.BoardMessageId;

var mapStatsMessageId =
    configuredMapStatsMessageId
    ?? discordState.MapStatsMessageId;

var awardsMessageId =
    configuredAwardsMessageId
    ?? discordState.AwardsMessageId;

var tiltWatchMessageId =
    configuredTiltWatchMessageId
    ?? discordState.TiltWatchMessageId;





// --------------------------------------------------

// HÄMTA SPELARE

// --------------------------------------------------



var nicknames =

    playersConfig.Split(

        ',',

        StringSplitOptions.RemoveEmptyEntries |

        StringSplitOptions.TrimEntries);



var fetchedPlayers =

    new List<FetchedPlayerData>();



var allPlayerStats =

    new List<FaceitStatsResponse>();



foreach (var nickname in nicknames)

{

    try

    {

        Console.WriteLine(

            $"Hämtar {nickname}...");



        var player =

            await faceitClient.GetPlayerAsync(

                nickname);



        if (player is null)

        {

            Console.WriteLine(

                $"Ingen FACEIT-spelare hittades för {nickname}.");



            continue;

        }



        if (!player.Games.TryGetValue(

                "cs2",

                out var cs2))

        {

            Console.WriteLine(

                $"{nickname} saknar CS2-data.");



            continue;

        }



        // Hämta 50 matcher en gång.

        var fullStats =

            await faceitClient.GetRecentStatsAsync(

                player.PlayerId,

                MapStatsMatchLimit);



        var activityMatches =

            await faceitClient.GetMatchCountAsync(

                player.PlayerId,

                ActivityDays);



        allPlayerStats.Add(

            fullStats);



        fetchedPlayers.Add(

            new FetchedPlayerData

            {

                Player = player,

                Game = cs2,

                FullStats = fullStats,

                ActivityMatches = activityMatches

            });

    }

    catch (Exception ex)

    {

        Console.WriteLine(

            $"FEL för {nickname}: {ex.Message}");

    }

}



if (fetchedPlayers.Count == 0)

{

    throw new Exception(

        "Inga FACEIT-spelare kunde hämtas.");

}



// --------------------------------------------------

// GRUPPMATCHER

// --------------------------------------------------



var qualifiedMatchTeams =

    groupMatchFilterService.FindQualifiedMatchTeams(

        allPlayerStats,

        MinimumGroupPlayersPerMatch);



Console.WriteLine();

Console.WriteLine(

    $"👥 Hittade {qualifiedMatchTeams.Count} match/lag-kombinationer " +

    $"med minst {MinimumGroupPlayersPerMatch} spelare från gruppen.");



// --------------------------------------------------

// POWER RANKING + FACEIT ADVANCED

// --------------------------------------------------



var allPlayers =

    new List<PlayerLeaderboardEntry>();



var groupPlayerStats =

    new List<FaceitStatsResponse>();



var recentGroupStatsByPlayerId =

    new Dictionary<string, FaceitStatsResponse>(

        StringComparer.OrdinalIgnoreCase);



foreach (var fetchedPlayer in fetchedPlayers)

{

    // Kartstatistik: alla kvalificerade gruppmatcher bland de hämtade 50.

    var fullGroupStats =

        groupMatchFilterService.Filter(

            fetchedPlayer.FullStats,

            qualifiedMatchTeams);



    groupPlayerStats.Add(

        fullGroupStats);



    // Power Ranking: de senaste 10 matcherna totalt,

    // men endast kvalificerade gruppmatcher räknas.

    var latestTen =

        TakeFirstMatches(

            fetchedPlayer.FullStats,

            PlayerStatsMatchLimit);



    var recentGroupStats =

        groupMatchFilterService.Filter(

            latestTen,

            qualifiedMatchTeams);



    recentGroupStatsByPlayerId[

        fetchedPlayer.Player.PlayerId] =

        recentGroupStats;



    var calculated =

        CalculateStats(

            recentGroupStats);



    Console.WriteLine(

        $"👥 {fetchedPlayer.Player.Nickname}: " +

        $"{recentGroupStats.Items.Count}/{latestTen.Items.Count} " +

        "av de senaste matcherna räknas som gruppmatcher.");



    allPlayers.Add(

        new PlayerLeaderboardEntry

        {

            PlayerId =

                fetchedPlayer.Player.PlayerId,



            Name =

                fetchedPlayer.Player.Nickname,



            Elo =

                fetchedPlayer.Game.Elo,



            Level =

                fetchedPlayer.Game.SkillLevel,



            Matches =

                calculated.Matches,



            ActivityMatches =

                fetchedPlayer.ActivityMatches,



            Wins =

                calculated.Wins,



            Losses =

                calculated.Losses,



            WinRate =

                calculated.WinRate,



            Kd =

                calculated.Kd,



            Kr =

                calculated.Kr,



            AverageKills =

                calculated.AverageKills,



            Adr =

                calculated.Adr,



            HeadshotPercentage =

                calculated.HeadshotPercentage,



            Streak =

                calculated.Streak,



            TotalKills =

                calculated.TotalKills,



            TotalMvps =

                calculated.TotalMvps,



            AverageMvps =

                calculated.AverageMvps,



            TripleKills =

                calculated.TripleKills,



            QuadroKills =

                calculated.QuadroKills,



            PentaKills =

                calculated.PentaKills

        });

}



var advancedInputs =

    allPlayers

        .Where(x =>

            x.Matches >= MinimumGroupMatchesForRanking)

        .Select(x =>

        {

            var stats =

                recentGroupStatsByPlayerId[x.PlayerId];



            var matchIds =

                stats.Items

                    .Select(item => item.Stats.MatchId)

                    .Where(matchId =>

                        !string.IsNullOrWhiteSpace(matchId))

                    .Distinct(StringComparer.OrdinalIgnoreCase)

                    .ToList();



            return new FaceitAdvancedStatsPlayerInput

            {

                PlayerId = x.PlayerId,

                Name = x.Name,

                MatchIds = matchIds

            };

        })

        .ToList();



var advancedStatsByPlayerId =

    await faceitAdvancedStatsService.CalculateAsync(

        advancedInputs);



foreach (var player in allPlayers)

{

    if (!advancedStatsByPlayerId.TryGetValue(

            player.PlayerId,

            out var advanced))

    {

        continue;

    }



    player.AdvancedMatches =

        advanced.AnalyzedMatches;



    player.TotalEntryAttempts =

        advanced.TotalEntryAttempts;



    player.EntryAttemptsPerMatch =

        advanced.AverageEntryAttemptsPerMatch;



    player.EntryKillsPerMatch =

        advanced.AverageEntryKillsPerMatch;



    player.EntrySuccessPercentage =

        advanced.EntrySuccessPercentage;



    player.EntryRatePercentage =

        advanced.EntryRatePercentage;



    player.ClutchAttempts =

        advanced.TotalClutchAttempts;



    player.ClutchWinPercentage =

        advanced.ClutchWinPercentage;



    player.UtilityDamagePerRound =

        advanced.UtilityDamagePerRound;



    player.EnemiesFlashedPerRound =

        advanced.EnemiesFlashedPerRound;



    player.FlashSuccessPercentage =

        advanced.FlashSuccessPercentage;

}



CalculateCowardiceIndexes(

    allPlayers

        .Where(x =>

            x.Matches >= MinimumGroupMatchesForRanking &&

            x.AdvancedMatches > 0)

        .ToList());



// --------------------------------------------------

// ELO-HISTORIK

// --------------------------------------------------



foreach (var player in allPlayers)

{

    player.EloDelta7Days =

        historyService.GetEloDelta(

            player.Name,

            player.Elo,

            7);



    historyService.AddSnapshot(

        player.Name,

        player.Elo);

}



await historyService.SaveAsync();





// --------------------------------------------------

// RANKING

// --------------------------------------------------



var rankingPlayers =

    allPlayers

        .Where(

            x => x.Matches >= MinimumGroupMatchesForRanking)

        .ToList();



var excludedPlayers =

    allPlayers

        .Where(

            x => x.Matches < MinimumGroupMatchesForRanking)

        .ToList();



foreach (var excludedPlayer in excludedPlayers)

{

    Console.WriteLine(

        $"🚫 {excludedPlayer.Name}: filtreras bort från Power Ranking och utmärkelser " +

        $"({excludedPlayer.Matches}/{PlayerStatsMatchLimit} gruppmatcher, " +

        $"minst {MinimumGroupMatchesForRanking} krävs)." );

}



var orderedPlayers =

    rankingPlayers

        .OrderByDescending(

            x => x.Elo)

        .ToList();





static string FormatExcludedPlayers(

    IReadOnlyList<PlayerLeaderboardEntry> excludedPlayers,

    int rankingWindow)

{

    return string.Join(

        ", ",

        excludedPlayers

            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)

            .Select(

                x => $"{EscapeDiscordMarkdown(x.Name)} ({x.Matches}/{rankingWindow})"));

}




// --------------------------------------------------

// KARTSTATISTIK

// --------------------------------------------------



var mapStats =

    mapStatsService.Calculate(

        groupPlayerStats);



var mapStatsMessage =

    BuildMapStatistics(

        mapStats,

        MapStatsMatchLimit);





// --------------------------------------------------

// TILT WATCH

// --------------------------------------------------



var tiltWatchPlayer =

    orderedPlayers

        .Where(

            x => x.Matches > 0)

        .OrderByDescending(

            x => x.Losses)

        .ThenBy(

            x => x.WinRate)

        .ThenBy(

            x => x.Kd)

        .FirstOrDefault();





// --------------------------------------------------

// HUVUDTAVLA

// --------------------------------------------------



var leaderboardMessage =

    BuildLeaderboard(

        orderedPlayers,

        MinimumGroupMatchesForRanking,

        PlayerStatsMatchLimit,

        excludedPlayers);



var awardsMessage =

    BuildAwardsMessage(

        orderedPlayers,

        ActivityDays,

        MinimumGroupMatchesForAwards,

        PlayerStatsMatchLimit);



var returnedBoardMessageId =

    await discordClient.PublishBoardAsync(

        content:

            leaderboardMessage,

        messageId:

            boardMessageId);



discordState.BoardMessageId =

    returnedBoardMessageId;



await discordStateService.SaveAsync(

    discordState);



// --------------------------------------------------

// GRISARNAS UTMÄRKELSER

// --------------------------------------------------



var returnedAwardsMessageId =

    await discordClient.PublishAwardsAsync(

        content:

            awardsMessage,

        messageId:

            awardsMessageId);



discordState.AwardsMessageId =

    returnedAwardsMessageId;



await discordStateService.SaveAsync(

    discordState);





// --------------------------------------------------

// KARTSTATISTIK

// --------------------------------------------------



var returnedMapStatsMessageId =

    await discordClient.PublishMapStatsAsync(

        content:

            mapStatsMessage,

        messageId:

            mapStatsMessageId);



discordState.MapStatsMessageId =

    returnedMapStatsMessageId;



await discordStateService.SaveAsync(

    discordState);







// --------------------------------------------------

// TILT WATCH

// --------------------------------------------------



string? returnedTiltWatchMessageId =

    tiltWatchMessageId;



if (tiltWatchPlayer is not null)

{

    var tiltDescription =

        $"**Tiltvarning: {EscapeDiscordMarkdown(tiltWatchPlayer.Name)}**\n" +

        $"📉 **{tiltWatchPlayer.Losses} förluster på " +

        $"{tiltWatchPlayer.Matches} matcher** — " +

        $"⚠️ Ytterligare matcher kan leda till tilt.";



    returnedTiltWatchMessageId =

        await discordClient.PublishAwardWithImageAsync(

            title:

                "🧊 TILT WATCH",

            description:

                tiltDescription,

            imagePath:

                "assets/fena.jpg",

            messageId:

                tiltWatchMessageId);



    discordState.TiltWatchMessageId =

        returnedTiltWatchMessageId;



    await discordStateService.SaveAsync(

        discordState);

}





// --------------------------------------------------

// KLART

// --------------------------------------------------



Console.WriteLine();



Console.WriteLine(

    "✅ GRIS-tavlan är klar.");



Console.WriteLine();



Console.WriteLine(

    $"Board ID: {returnedBoardMessageId}");



Console.WriteLine(

    $"Awards ID: {returnedAwardsMessageId}");



Console.WriteLine(

    $"Map ID: {returnedMapStatsMessageId}");



Console.WriteLine(

    $"Tilt Watch ID: {returnedTiltWatchMessageId}");





// --------------------------------------------------

// SENASTE X MATCHER

// --------------------------------------------------



static FaceitStatsResponse TakeFirstMatches(

    FaceitStatsResponse response,

    int count)

{

    return new FaceitStatsResponse

    {

        Items =

            response.Items

                .Take(count)

                .ToList()

    };

}





// --------------------------------------------------

// SPELARSTATISTIK

// --------------------------------------------------



static CalculatedStats CalculateStats(

    FaceitStatsResponse response)

{

    var matches =

        response.Items

            .Select(x => x.Stats)

            .ToList();



    if (matches.Count == 0)

    {

        return new CalculatedStats();

    }



    var wins =

        matches.Count(

            x => x.Result == "1");



    var losses =

        matches.Count - wins;



    var kills =

        matches.Sum(

            x => ToInt(x.Kills));



    var deaths =

        matches.Sum(

            x => ToInt(x.Deaths));



    var rounds =

        matches.Sum(

            x => ToInt(x.Rounds));



    var headshots =

        matches.Sum(

            x => ToInt(x.Headshots));



    var mvps =

        matches.Sum(

            x => ToInt(x.Mvps));



    var tripleKills =

        matches.Sum(

            x => ToInt(x.TripleKills));



    var quadroKills =

        matches.Sum(

            x => ToInt(x.QuadroKills));



    var pentaKills =

        matches.Sum(

            x => ToInt(x.PentaKills));



    var averageKills =

        (double)kills /

        matches.Count;



    var averageMvps =

        (double)mvps /

        matches.Count;



    var kd =

        deaths == 0

            ? kills

            : (double)kills /

              deaths;



    var kr =

        rounds == 0

            ? 0

            : (double)kills /

              rounds;



    var headshotPercentage =

        kills == 0

            ? 0

            : (double)headshots /

              kills * 100;



    var adr =

        matches.Average(

            x => ToDouble(x.Adr));



    var winRate =

        (double)wins /

        matches.Count * 100;



    var streak =

        CalculateStreak(

            matches);



    return new CalculatedStats

    {

        Matches =

            matches.Count,



        Wins =

            wins,



        Losses =

            losses,



        TotalKills =

            kills,



        TotalMvps =

            mvps,



        AverageMvps =

            averageMvps,



        WinRate =

            winRate,



        Kd =

            kd,



        Kr =

            kr,



        AverageKills =

            averageKills,



        Adr =

            adr,



        HeadshotPercentage =

            headshotPercentage,



        Streak =

            streak,



        TripleKills =

            tripleKills,



        QuadroKills =

            quadroKills,



        PentaKills =

            pentaKills

    };

}



static int CalculateStreak(

    IReadOnlyList<FaceitMatchStats> matches)

{

    if (matches.Count == 0)

        return 0;



    var firstResult =

        matches[0].Result;



    var count =

        0;



    foreach (var match in matches)

    {

        if (match.Result != firstResult)

            break;



        count++;

    }



    return firstResult == "1"

        ? count

        : -count;

}





// --------------------------------------------------

// POWER RANKING

// --------------------------------------------------



static string BuildLeaderboard(
    IReadOnlyList<PlayerLeaderboardEntry> players,
    int minimumGroupMatches,
    int rankingWindow,
    IReadOnlyList<PlayerLeaderboardEntry> excludedPlayers)
{
    var sb = new StringBuilder();

    if (players.Count == 0)
    {
        sb.AppendLine(
            $"Ingen har minst {minimumGroupMatches} gruppmatcher " +
            $"bland sina senaste {rankingWindow} FACEIT-matcher.");

        if (excludedPlayers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"🚫 **Ej med:** " +
                $"{FormatExcludedPlayers(excludedPlayers, rankingWindow)}");
        }

        sb.AppendLine();
        sb.AppendLine(
            $"🕐 Uppdaterad <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");

        return sb.ToString();
    }

    for (var i = 0; i < players.Count; i++)
    {
        var player = players[i];
        var displayName = EscapeDiscordMarkdown(player.Name);

        var medal = i switch
        {
            0 => "🥇",
            1 => "🥈",
            2 => "🥉",
            _ => "🔹"
        };

        sb.AppendLine($"{medal} **{displayName}**");
        sb.AppendLine(
            $"> LVL **{player.Level}** • " +
            $"**{player.Elo} ELO** " +
            $"{FormatEloDelta(player.EloDelta7Days)}");
        sb.AppendLine(
            $"> 📊 {player.Wins}V-{player.Losses}F ({player.Matches}) • " +
            $"**{player.WinRate:0}%** • " +
            $"⚔️ {player.Kd:0.00} K/D • " +
            $"💥 {player.Adr:0.0} ADR • " +
            $"🎯 {player.HeadshotPercentage:0}% HS" +
            $"{FormatStreak(player.Streak)}");

        if (player.AdvancedMatches > 0)
        {
            sb.AppendLine(
                $"> 🚪 {player.EntryKillsPerMatch:0.00}/m • " +
                $"⚡ {player.EntryAttemptsPerMatch:0.00}/m • " +
                $"✅ {player.EntrySuccessPercentage:0}% • " +
                $"🧠 {FormatClutch(player)} • " +
                $"💣 {player.UtilityDamagePerRound:0.0}/r • " +
                $"💡 {player.EnemiesFlashedPerRound:0.00}/r • " +
                $"{FormatPlayStyle(player)}" +
                $"{FormatAdvancedCoverage(player)}");
        }
        else
        {
            sb.AppendLine("> 🧠 Advanced-data saknas.");
        }

        sb.AppendLine();
    }

    sb.AppendLine(
        $"*Senaste {rankingWindow} FACEIT-matcherna • minst {minimumGroupMatches} gruppmatcher.*");

    sb.AppendLine(
        "*🚪 entry kills/m • ⚡ entrydueller/m • 🧠 clutch • 💣 util/r • 💡 flashade/r.*");

    sb.AppendLine(
        "*Spelstil: 🦍 offensiv → ⚖️ balanserad → 🐔 avvaktande. " +
        "Indexet bygger på entry rate; 50 ≈ normalnivå.*");

    if (excludedPlayers.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine(
            $"🚫 **Ej med:** " +
            $"{FormatExcludedPlayers(excludedPlayers, rankingWindow)}");
    }

    sb.AppendLine();
    sb.AppendLine(
        $"🕐 Uppdaterad <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");

    return sb.ToString();
}


static string BuildAwardsMessage(
    IReadOnlyList<PlayerLeaderboardEntry> players,
    int activityDays,
    int minimumGroupMatchesForAwards,
    int rankingWindow)
{
    var sb = new StringBuilder();

    var awardPlayers = players
        .Where(x => x.Matches >= minimumGroupMatchesForAwards)
        .ToList();

    var awardIneligiblePlayers = players
        .Where(x => x.Matches < minimumGroupMatchesForAwards)
        .ToList();

    if (awardPlayers.Count == 0)
    {
        sb.AppendLine(
            $"Ingen har minst {minimumGroupMatchesForAwards} gruppmatcher ännu.");
    }
    else
    {
        var eloKing = awardPlayers.MaxBy(x => x.Elo)!;
        var bestKd = awardPlayers.MaxBy(x => x.Kd)!;
        var bestWinRate = awardPlayers.MaxBy(x => x.WinRate)!;
        var aimKing = awardPlayers.MaxBy(x => x.HeadshotPercentage)!;
        var fragMachine = awardPlayers.MaxBy(x => x.AverageKills)!;
        var damageDealer = awardPlayers.MaxBy(x => x.Adr)!;
        var lowestAdr = awardPlayers.MinBy(x => x.Adr)!;
        var mvpFarmer = awardPlayers.MaxBy(x => x.AverageMvps)!;
        var lowestHs = awardPlayers.MinBy(x => x.HeadshotPercentage)!;
        var lowestMvps = awardPlayers.MinBy(x => x.AverageMvps)!;
        var lowestWinRate = awardPlayers.MinBy(x => x.WinRate)!;
        var lowestElo = awardPlayers.MinBy(x => x.Elo)!;

        var hottest = awardPlayers
            .Where(x => x.Streak >= 2)
            .OrderByDescending(x => x.Streak)
            .FirstOrDefault();

        var playersWithHistory = awardPlayers
            .Where(x => x.EloDelta7Days.HasValue)
            .ToList();

        var stonks = playersWithHistory
            .OrderByDescending(x => x.EloDelta7Days)
            .FirstOrDefault();

        var eloDonator = playersWithHistory
            .OrderBy(x => x.EloDelta7Days)
            .FirstOrDefault();

        var mostActive = awardPlayers
            .OrderByDescending(x => x.ActivityMatches)
            .First();

        var leastActive = awardPlayers
            .OrderBy(x => x.ActivityMatches)
            .First();

        AppendAward(
            sb,
            "👑",
            "ELO-KUNGEN",
            $"{EscapeDiscordMarkdown(eloKing.Name)} — {eloKing.Elo} ELO");

        AppendAward(
            sb,
            "⚔️",
            "K/D-DEMONEN",
            $"{EscapeDiscordMarkdown(bestKd.Name)} — {bestKd.Kd:0.00} K/D");

        AppendAward(
            sb,
            "📈",
            "VINSTMASKINEN",
            $"{EscapeDiscordMarkdown(bestWinRate.Name)} — {bestWinRate.WinRate:0}% vinst");

        AppendAward(
            sb,
            "🎯",
            "AIM-KUNGEN",
            $"{EscapeDiscordMarkdown(aimKing.Name)} — {aimKing.HeadshotPercentage:0}% HS");

        AppendAward(
            sb,
            "💣",
            "FRAGMASKINEN",
            $"{EscapeDiscordMarkdown(fragMachine.Name)} — {fragMachine.AverageKills:0.0} kills/match");

        AppendAward(
            sb,
            "💥",
            "SKADEMASKINEN",
            $"{EscapeDiscordMarkdown(damageDealer.Name)} — {damageDealer.Adr:0.0} ADR");

        AppendAward(
            sb,
            "⭐",
            "MVP-BONDEN",
            $"{EscapeDiscordMarkdown(mvpFarmer.Name)} — {mvpFarmer.AverageMvps:0.00} MVP/match");

        if (stonks is not null && stonks.EloDelta7Days > 0)
        {
            AppendAward(
                sb,
                "🚀",
                "STONKS",
                $"{EscapeDiscordMarkdown(stonks.Name)} — +{stonks.EloDelta7Days} ELO");
        }

        if (eloDonator is not null && eloDonator.EloDelta7Days < 0)
        {
            AppendAward(
                sb,
                "📉",
                "ELO DONATORN",
                $"{EscapeDiscordMarkdown(eloDonator.Name)} — {eloDonator.EloDelta7Days} ELO");
        }

        if (hottest is not null)
        {
            AppendAward(
                sb,
                "🔥",
                "GLÖDHET",
                $"{EscapeDiscordMarkdown(hottest.Name)} — {hottest.Streak} raka vinster");
        }

        AppendAward(
            sb,
            "🦟",
            "MYGGBETTET",
            $"{EscapeDiscordMarkdown(lowestAdr.Name)} — {lowestAdr.Adr:0.0} ADR");

        AppendAward(
            sb,
            "🙈",
            "Låg HS%, hög sannolikhet för AWP missbruk",
            $"{EscapeDiscordMarkdown(lowestHs.Name)} — {lowestHs.HeadshotPercentage:0}% HS");

        AppendAward(
            sb,
            "😴",
            "MVP ALLERGI",
            $"{EscapeDiscordMarkdown(lowestMvps.Name)} — {lowestMvps.AverageMvps:0.00} MVP/match");

        AppendAward(
            sb,
            "🚨",
            "FORMKRIS",
            $"{EscapeDiscordMarkdown(lowestWinRate.Name)} — {lowestWinRate.WinRate:0}% vinst");

        AppendAward(
            sb,
            "🚓",
            "UNDER UTREDNING",
            $"{EscapeDiscordMarkdown(lowestElo.Name)} — {lowestElo.Elo} ELO");

        AppendAward(
            sb,
            "🎮",
            "ARBETSLÖSA KRIGAREN",
            $"{EscapeDiscordMarkdown(mostActive.Name)} — " +
            $"{mostActive.ActivityMatches} matcher senaste {activityDays} dagarna");

        AppendAward(
            sb,
            "🛋️",
            "SOFFGENERALEN",
            $"{EscapeDiscordMarkdown(leastActive.Name)} — " +
            $"{leastActive.ActivityMatches} matcher senaste {activityDays} dagarna");

        var advancedAwardPlayers =
            awardPlayers
                .Where(x =>
                    x.AdvancedMatches >= minimumGroupMatchesForAwards)
                .ToList();

        if (advancedAwardPlayers.Count > 0)
        {
            var firstIn =
                advancedAwardPlayers
                    .MaxBy(x => x.EntryAttemptsPerMatch)!;

            var doorKicker =
                advancedAwardPlayers
                    .MaxBy(x => x.EntryKillsPerMatch)!;

            var entrySuccessCandidates =
                advancedAwardPlayers
                    .Where(x => x.TotalEntryAttempts >= 5)
                    .ToList();

            var duelKing =
                entrySuccessCandidates
                    .MaxBy(x => x.EntrySuccessPercentage);

            var clutchCandidates =
                advancedAwardPlayers
                    .Where(x =>
                        x.ClutchAttempts >= 2 &&
                        x.ClutchWinPercentage.HasValue)
                    .ToList();

            var clutchKing =
                clutchCandidates
                    .MaxBy(x => x.ClutchWinPercentage);

            var grenadeMaster =
                advancedAwardPlayers
                    .MaxBy(x => x.UtilityDamagePerRound)!;

            var flashMaster =
                advancedAwardPlayers
                    .MaxBy(x => x.EnemiesFlashedPerRound)!;

            var backlineOperator =
                advancedAwardPlayers
                    .Where(x => x.CowardiceIndex.HasValue)
                    .MaxBy(x => x.CowardiceIndex);

            AppendAward(
                sb,
                "🦍",
                "FÖRST IN SIST UT",
                $"{EscapeDiscordMarkdown(firstIn.Name)} — " +
                $"{firstIn.EntryAttemptsPerMatch:0.00} entrydueller/match");

            AppendAward(
                sb,
                "🚪",
                "DÖRRSPARKAREN",
                $"{EscapeDiscordMarkdown(doorKicker.Name)} — " +
                $"{doorKicker.EntryKillsPerMatch:0.00} entry kills/match");

            if (duelKing is not null)
            {
                AppendAward(
                    sb,
                    "🎯",
                    "ENTRY-DUELLKUNGEN",
                    $"{EscapeDiscordMarkdown(duelKing.Name)} — " +
                    $"{duelKing.EntrySuccessPercentage:0}% vunna entrydueller");
            }

            if (clutchKing is not null)
            {
                AppendAward(
                    sb,
                    "🧠",
                    "CLUTCHKUNGEN",
                    $"{EscapeDiscordMarkdown(clutchKing.Name)} — " +
                    $"{clutchKing.ClutchWinPercentage!.Value:0}% " +
                    $"({clutchKing.ClutchAttempts} försök)");
            }

            AppendAward(
                sb,
                "💣",
                "SPRÄNGMÄSTAREN",
                $"{EscapeDiscordMarkdown(grenadeMaster.Name)} — " +
                $"{grenadeMaster.UtilityDamagePerRound:0.0} utility damage/runda");

            AppendAward(
                sb,
                "💡",
                "BLÄNDVERKET",
                $"{EscapeDiscordMarkdown(flashMaster.Name)} — " +
                $"{flashMaster.EnemiesFlashedPerRound:0.00} fiender flashade/runda");

            if (backlineOperator is not null)
            {
                AppendAward(
                    sb,
                    "🐔",
                    "BAKRADSOPERATÖREN",
                    $"{EscapeDiscordMarkdown(backlineOperator.Name)} — " +
                    $"{backlineOperator.CowardiceIndex}/100");
            }
        }
    }

    sb.AppendLine(
        $"*Minst {minimumGroupMatchesForAwards} gruppmatcher av senaste {rankingWindow} för utmärkelser.*");

    if (awardIneligiblePlayers.Count > 0)
    {
        sb.AppendLine(
            $"🚫 **Ej med:** " +
            $"{FormatExcludedPlayers(awardIneligiblePlayers, rankingWindow)}");
    }

    sb.AppendLine();
    sb.AppendLine(
        $"🕐 Uppdaterad <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");

    return sb.ToString();
}


static void CalculateCowardiceIndexes(
    IReadOnlyList<PlayerLeaderboardEntry> players)
{
    foreach (var player in players)
    {
        // Fast skala i stället för relativ ranking inom gruppen.
        // 20% entry rate = ungefär mittpunkten 50.
        // 40%+ är mycket offensivt, nära 0% mycket avvaktande.
        var rawIndex =
            100 - player.EntryRatePercentage * 2.5;

        player.CowardiceIndex =
            (int)Math.Round(
                Math.Clamp(rawIndex, 5, 95),
                MidpointRounding.AwayFromZero);
    }
}


static string FormatClutch(
    PlayerLeaderboardEntry player)
{
    if (!player.ClutchWinPercentage.HasValue)
        return "–";

    return $"{player.ClutchWinPercentage.Value:0}%";
}


static string FormatAdvancedCoverage(
    PlayerLeaderboardEntry player)
{
    if (player.AdvancedMatches >= player.Matches)
        return "";

    return $" • ⚠️ {player.AdvancedMatches}/{player.Matches} adv";
}


static string FormatPlayStyle(
    PlayerLeaderboardEntry player)
{
    if (!player.CowardiceIndex.HasValue)
        return "🐔–";

    var index =
        player.CowardiceIndex.Value;

    var emoji =
        index switch
        {
            <= 25 => "🦍",
            <= 45 => "🔥",
            <= 65 => "⚖️",
            <= 80 => "🐢",
            _ => "🐔"
        };

    return $"{emoji}{index}";
}


static string EscapeDiscordMarkdown(string value)
{
    if (string.IsNullOrEmpty(value))
        return value;

    return value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("*", "\\*", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal)
        .Replace("~", "\\~", StringComparison.Ordinal)
        .Replace("`", "\\`", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal);
}


// --------------------------------------------------

// KARTSTATISTIK

// --------------------------------------------------



static string BuildMapStatistics(

    IReadOnlyList<MapStatistics> maps,

    int matchesPerPlayer)

{

    var sb =

        new StringBuilder();



    // Säkerställ ordningen även här.

    var orderedMaps =

        maps

            .OrderByDescending(

                x => x.WinRate)

            .ThenByDescending(

                x => x.UniqueMatches)

            .ThenBy(

                x => x.Map)

            .ToList();



    if (orderedMaps.Count == 0)

    {

        sb.AppendLine(

            "Ingen karta har minst 10 unika matcher ännu.");



        sb.AppendLine();



        sb.AppendLine(

            $"🕐 Uppdaterad " +

            $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");



        return sb.ToString();

    }



    foreach (var map in orderedMaps)

    {

        sb.AppendLine(

            $"### 🗺️ {map.Map}");



        sb.AppendLine(

            $"> 🎮 **{map.UniqueMatches} unika matcher** • " +

            $"👥 {map.PlayerAppearances} spelarresultat");



        sb.AppendLine(

            $"> 🏆 **{map.WinRate:0}% vinst** • " +

            $"⚔️ {map.Kd:0.00} K/D • " +

            $"🔫 {map.Kr:0.00} K/R");



        sb.AppendLine(

            $"> 💥 {map.Adr:0.0} ADR • " +

            $"🎯 {map.HeadshotPercentage:0}% HS • " +

            $"💣 {map.AverageKills:0.0} kills/match");



        sb.AppendLine();

    }



    var homeMap =

        orderedMaps.First();



    var banThis =

        orderedMaps.Last();



    var bloodbath =

        orderedMaps.MaxBy(

            x => x.Adr)!;



    var hsParadise =

        orderedMaps.MaxBy(

            x => x.HeadshotPercentage)!;



    sb.AppendLine(

        "━━━━━━━━━━━━━━━━━━");



    sb.AppendLine(

        "### 🏅 KARTSTATISTIK");



    sb.AppendLine();



    AppendAward(

        sb,

        "🏰",

        "HEMMAPLAN",

        $"{homeMap.Map} — " +

        $"{homeMap.WinRate:0}% vinst");



    AppendAward(

        sb,

        "☠️",

        "BANNLYS SKITEN",

        $"{banThis.Map} — " +

        $"{banThis.WinRate:0}% vinst");



    AppendAward(

        sb,

        "💥",

        "BLODBADET",

        $"{bloodbath.Map} — " +

        $"{bloodbath.Adr:0.0} ADR");



    AppendAward(

        sb,

        "🎯",

        "HS-PARADISET",

        $"{hsParadise.Map} — " +

        $"{hsParadise.HeadshotPercentage:0}% HS");



    sb.AppendLine(

        $"*Kartstatistiken använder gruppmatcher bland upp till " +

        $"{matchesPerPlayer} senaste matcher per spelare.*");



    sb.AppendLine();



    sb.AppendLine(

        "*Endast kartor med minst 10 unika matcher visas.*");



    sb.AppendLine(

        "*Kartorna är sorterade från högst till lägst winrate.*");



    sb.AppendLine();



    sb.AppendLine(

        "*Om flera spelare i gruppen spelar samma match " +

        "räknas den som en unik match, men varje spelares " +

        "prestation räknas i gruppstatistiken.*");



    sb.AppendLine();



    sb.AppendLine(

        $"🕐 Uppdaterad " +

        $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");



    return sb.ToString();

}





// --------------------------------------------------

// GEMENSAM FORMATERING

// --------------------------------------------------



static void AppendAward(

    StringBuilder sb,

    string emoji,

    string title,

    string value)

{

    sb.AppendLine(

        $"{emoji} **{title}**");



    sb.AppendLine(

        value);



    sb.AppendLine();

}



static string FormatEloDelta(

    int? delta)

{

    if (delta is null)

    {

        return

            "• 🆕 spårning startad";

    }



    if (delta > 0)

    {

        return

            $"• 📈 **+{delta}** på 7 dagar";

    }



    if (delta < 0)

    {

        return

            $"• 📉 **{delta}** på 7 dagar";

    }



    return

        "• ➖ **0** på 7 dagar";

}



static string FormatStreak(

    int streak)

{

    if (streak >= 2)

    {

        return

            $" • 🔥 {streak} raka vinster";

    }



    if (streak <= -2)

    {

        return

            $" • 🧊 {Math.Abs(streak)} raka förluster";

    }



    return "";

}



static int ToInt(

    string? value)

{

    return int.TryParse(

        value,

        NumberStyles.Any,

        CultureInfo.InvariantCulture,

        out var result)

            ? result

            : 0;

}



static double ToDouble(

    string? value)

{

    return double.TryParse(

        value,

        NumberStyles.Any,

        CultureInfo.InvariantCulture,

        out var result)

            ? result

            : 0;

}





// --------------------------------------------------

// INTERNA MODELLER

// --------------------------------------------------



internal sealed class FetchedPlayerData

{

    public FaceitPlayer Player { get; init; } = new();



    public FaceitGame Game { get; init; } = new();



    public FaceitStatsResponse FullStats { get; init; } = new();



    public int ActivityMatches { get; init; }

}



internal sealed class CalculatedStats

{

    public int Matches { get; init; }



    public int Wins { get; init; }



    public int Losses { get; init; }



    public int TotalKills { get; init; }



    public int TotalMvps { get; init; }



    public double AverageMvps { get; init; }



    public double WinRate { get; init; }



    public double Kd { get; init; }



    public double Kr { get; init; }



    public double AverageKills { get; init; }



    public double Adr { get; init; }



    public double HeadshotPercentage { get; init; }



    public int Streak { get; init; }



    public int TripleKills { get; init; }



    public int QuadroKills { get; init; }



    public int PentaKills { get; init; }

}



internal sealed class PlayerLeaderboardEntry

{

    public string PlayerId { get; init; } = "";



    public string Name { get; init; } = "";



    public int Elo { get; init; }



    public int Level { get; init; }



    public int? EloDelta7Days { get; set; }



    public int Matches { get; init; }



    public int ActivityMatches { get; init; }



    public int Wins { get; init; }



    public int Losses { get; init; }



    public double WinRate { get; init; }



    public double Kd { get; init; }



    public double Kr { get; init; }



    public double AverageKills { get; init; }



    public double Adr { get; init; }



    public double HeadshotPercentage { get; init; }



    public int Streak { get; init; }



    public int TotalKills { get; init; }



    public int TotalMvps { get; init; }



    public double AverageMvps { get; init; }



    public int AdvancedMatches { get; set; }



    public int TotalEntryAttempts { get; set; }



    public double EntryAttemptsPerMatch { get; set; }



    public double EntryKillsPerMatch { get; set; }



    public double EntrySuccessPercentage { get; set; }



    public double EntryRatePercentage { get; set; }



    public int ClutchAttempts { get; set; }



    public double? ClutchWinPercentage { get; set; }



    public double UtilityDamagePerRound { get; set; }



    public double EnemiesFlashedPerRound { get; set; }



    public double FlashSuccessPercentage { get; set; }



    public int? CowardiceIndex { get; set; }



    public int TripleKills { get; init; }



    public int QuadroKills { get; init; }



    public int PentaKills { get; init; }

}