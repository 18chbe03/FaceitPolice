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



var leetifyApiKey =

    Environment.GetEnvironmentVariable(

        "LEETIFY_API_KEY");



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



var leetifyClient =

    new LeetifyClient(

        httpClient,

        leetifyApiKey);



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



var allPlayers =

    new List<PlayerLeaderboardEntry>();



var groupPlayerStats =

    new List<FaceitStatsResponse>();



foreach (var fetchedPlayer in fetchedPlayers)

{

    // Kartstatistik: filtrera de senaste 50 matcherna.

    var fullGroupStats =

        groupMatchFilterService.Filter(

            fetchedPlayer.FullStats,

            qualifiedMatchTeams);



    groupPlayerStats.Add(

        fullGroupStats);



    // Power Ranking: utgå fortfarande från de senaste 10

    // matcherna totalt, men räkna bara gruppmatcher i det fönstret.

    var latestTen =

        TakeFirstMatches(

            fetchedPlayer.FullStats,

            PlayerStatsMatchLimit);



    var recentGroupStats =

        groupMatchFilterService.Filter(

            latestTen,

            qualifiedMatchTeams);



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





// --------------------------------------------------

// LEETIFY TEST - EN AV DE SENASTE GRUPPMATCHERNA

// --------------------------------------------------



var recentGroupMatchIds =

    groupPlayerStats

        .SelectMany(x => x.Items)

        .Select(x => x.Stats)

        .Where(x =>

            !string.IsNullOrWhiteSpace(x.MatchId))

        .GroupBy(

            x => x.MatchId,

            StringComparer.OrdinalIgnoreCase)

        .Select(group => new

        {

            MatchId = group.Key,

            FinishedAt = group.Max(x => x.MatchFinishedAt)

        })

        .OrderByDescending(x => x.FinishedAt)

        .Select(x => x.MatchId)

        .ToList();



await leetifyClient.TryLogFirstAvailableFaceitMatchAsync(

    recentGroupMatchIds,

    maxAttempts: 5);



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

        ActivityDays,

        MinimumGroupMatchesForRanking,

        MinimumGroupMatchesForAwards,

        PlayerStatsMatchLimit,

        excludedPlayers);



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

    "✅ FACEIT-tavlan är klar.");



Console.WriteLine();



Console.WriteLine(

    $"Board ID: {returnedBoardMessageId}");



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
    int activityDays,
    int minimumGroupMatches,
    int minimumGroupMatchesForAwards,
    int rankingWindow,
    IReadOnlyList<PlayerLeaderboardEntry> excludedPlayers)
{
    var sb = new StringBuilder();

    if (players.Count == 0)
    {
        sb.AppendLine(
            $"Ingen spelare har minst {minimumGroupMatches} gruppmatcher " +
            $"bland sina senaste {rankingWindow} FACEIT-matcher.");

        if (excludedPlayers.Count > 0)
        {
            sb.AppendLine(
                $"🚫 **Ej kvalificerade för Power Ranking:** " +
                $"{FormatExcludedPlayers(excludedPlayers, rankingWindow)}");
            sb.AppendLine();
        }

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
            $"> 📊 {player.Wins}V-{player.Losses}F " +
            $"({player.Matches} gruppmatcher) • " +
            $"**{player.WinRate:0}% vinst** • " +
            $"⚔️ {player.Kd:0.00} K/D");
        sb.AppendLine(
            $"> 💣 {player.AverageKills:0.0} AVG • " +
            $"💥 {player.Adr:0.0} ADR • " +
            $"🎯 {player.HeadshotPercentage:0}% HS" +
            $"{FormatStreak(player.Streak)}");
        sb.AppendLine();
    }

    sb.AppendLine("━━━━━━━━━━━━━━━━━━");
    sb.AppendLine("### 🏅 GRUPPENS UTMÄRKELSER");
    sb.AppendLine();

    var awardPlayers = players
        .Where(x => x.Matches >= minimumGroupMatchesForAwards)
        .ToList();

    var awardIneligiblePlayers = players
        .Where(x => x.Matches < minimumGroupMatchesForAwards)
        .ToList();

    if (awardPlayers.Count == 0)
    {
        sb.AppendLine(
            $"*Ingen spelare har minst {minimumGroupMatchesForAwards} gruppmatcher, " +
            "så inga utmärkelser delas ut ännu.*");
        sb.AppendLine();
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
        var walkingDonation = awardPlayers.MinBy(x => x.Kd)!;
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
                "ELO-DONATORN",
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
            "SIKTESFÖRBUD",
            $"{EscapeDiscordMarkdown(lowestHs.Name)} — {lowestHs.HeadshotPercentage:0}% HS");

        AppendAward(
            sb,
            "😴",
            "MVP-ALLERGI",
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
    }

    sb.AppendLine(
        $"*Prestationsstatistik baserad på gruppmatcher bland de senaste " +
        $"{rankingWindow} FACEIT-matcherna. Minst {minimumGroupMatches} " +
        $"gruppmatcher krävs för att visas och minst {minimumGroupMatchesForAwards} " +
        "krävs för att kunna få en utmärkelse.*");

    sb.AppendLine(
        $"*Aktivitet baserad på matcher de senaste {activityDays} dagarna.*");

    if (awardIneligiblePlayers.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine(
            $"🏅 **Visas men ej kvalificerade för utmärkelser:** " +
            $"{FormatExcludedPlayers(awardIneligiblePlayers, rankingWindow)}");
    }

    if (excludedPlayers.Count > 0)
    {
        sb.AppendLine();
        sb.AppendLine(
            $"🚫 **Ej kvalificerade för Power Ranking:** " +
            $"{FormatExcludedPlayers(excludedPlayers, rankingWindow)}");
    }

    sb.AppendLine();
    sb.AppendLine(
        $"🕐 Uppdaterad <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");

    return sb.ToString();
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



    public double AverageKills { get; init; }



    public double Adr { get; init; }



    public double HeadshotPercentage { get; init; }



    public int Streak { get; init; }



    public int TotalKills { get; init; }



    public int TotalMvps { get; init; }



    public double AverageMvps { get; init; }



    public int TripleKills { get; init; }



    public int QuadroKills { get; init; }



    public int PentaKills { get; init; }

}