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



var discordStateService =

    new DiscordMessageStateService();





// --------------------------------------------------

// LADDA STATE

// --------------------------------------------------



await historyService.LoadAsync();



var discordState =

    await discordStateService.LoadAsync();





// State-filen vinner över env.

// Env används som fallback första gången.

var boardMessageId =

    discordState.BoardMessageId

    ?? configuredBoardMessageId;



var mapStatsMessageId =

    discordState.MapStatsMessageId

    ?? configuredMapStatsMessageId;



var tiltWatchMessageId =

    discordState.TiltWatchMessageId

    ?? configuredTiltWatchMessageId;





// --------------------------------------------------

// HÄMTA SPELARE

// --------------------------------------------------



var nicknames =

    playersConfig.Split(

        ',',

        StringSplitOptions.RemoveEmptyEntries |

        StringSplitOptions.TrimEntries);



var players =

    new List<PlayerLeaderboardEntry>();



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



        allPlayerStats.Add(

            fullStats);



        // Ranking baseras endast på senaste 10.

        var recentStats =

            TakeFirstMatches(

                fullStats,

                PlayerStatsMatchLimit);



        var calculated =

            CalculateStats(

                recentStats);



        var activityMatches =

            await faceitClient.GetMatchCountAsync(

                player.PlayerId,

                ActivityDays);



        players.Add(

            new PlayerLeaderboardEntry

            {

                Name =

                    player.Nickname,



                Elo =

                    cs2.Elo,



                Level =

                    cs2.SkillLevel,



                Matches =

                    calculated.Matches,



                ActivityMatches =

                    activityMatches,



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



                TripleKills =

                    calculated.TripleKills,



                QuadroKills =

                    calculated.QuadroKills,



                PentaKills =

                    calculated.PentaKills

            });

    }

    catch (Exception ex)

    {

        Console.WriteLine(

            $"FEL för {nickname}: {ex.Message}");

    }

}



if (players.Count == 0)

{

    throw new Exception(

        "Inga FACEIT-spelare kunde hämtas.");

}





// --------------------------------------------------

// ELO-HISTORIK

// --------------------------------------------------



foreach (var player in players)

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



var orderedPlayers =

    players

        .OrderByDescending(

            x => x.Elo)

        .ToList();





// --------------------------------------------------

// KARTSTATISTIK

// --------------------------------------------------



var mapStats =

    mapStatsService.Calculate(

        allPlayerStats);



var mapStatsMessage =

    BuildMapStatistics(

        mapStats,

        MapStatsMatchLimit);





// --------------------------------------------------

// TILT WATCH

// --------------------------------------------------



var tiltWatchPlayer =

    orderedPlayers

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

        ActivityDays);



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

        $"**Tiltvarning: {tiltWatchPlayer.Name}**\n" +

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

    int activityDays)

{

    var sb =

        new StringBuilder();



    for (var i = 0;

         i < players.Count;

         i++)

    {

        var player =

            players[i];



        var medal =

            i switch

            {

                0 => "🥇",

                1 => "🥈",

                2 => "🥉",

                _ => "🔹"

            };



        sb.AppendLine(

            $"{medal} **{player.Name}**");



        sb.AppendLine(

            $"> LVL **{player.Level}** • " +

            $"**{player.Elo} ELO** " +

            $"{FormatEloDelta(player.EloDelta7Days)}");



        sb.AppendLine(

            $"> 📊 {player.Wins}V-{player.Losses}F • " +

            $"**{player.WinRate:0}% vinst** • " +

            $"⚔️ {player.Kd:0.00} K/D");



        sb.AppendLine(

            $"> 💣 {player.AverageKills:0.0} AVG • " +

            $"💥 {player.Adr:0.0} ADR • " +

            $"🎯 {player.HeadshotPercentage:0}% HS" +

            $"{FormatStreak(player.Streak)}");



        sb.AppendLine();

    }



    sb.AppendLine(

        "━━━━━━━━━━━━━━━━━━");



    sb.AppendLine(

        "### 🏅 POLISRAPPORT");



    sb.AppendLine();



    var eloKing =

        players.MaxBy(

            x => x.Elo)!;



    var bestKd =

        players.MaxBy(

            x => x.Kd)!;



    var bestWinRate =

        players.MaxBy(

            x => x.WinRate)!;



    var aimKing =

        players.MaxBy(

            x => x.HeadshotPercentage)!;



    var fragMachine =

        players.MaxBy(

            x => x.AverageKills)!;



    var damageDealer =

        players.MaxBy(

            x => x.Adr)!;



    var lowestAdr =

        players.MinBy(

            x => x.Adr)!;



    var mvpFarmer =

        players.MaxBy(

            x => x.TotalMvps)!;



    var walkingDonation =

        players.MinBy(

            x => x.Kd)!;



    var lowestHs =

        players.MinBy(

            x => x.HeadshotPercentage)!;



    var lowestMvps =

        players.MinBy(

            x => x.TotalMvps)!;



    var lowestWinRate =

        players.MinBy(

            x => x.WinRate)!;



    var lowestElo =

        players.MinBy(

            x => x.Elo)!;



    var hottest =

        players

            .Where(

                x => x.Streak >= 2)

            .OrderByDescending(

                x => x.Streak)

            .FirstOrDefault();



    var playersWithHistory =

        players

            .Where(

                x => x.EloDelta7Days.HasValue)

            .ToList();



    var stonks =

        playersWithHistory

            .OrderByDescending(

                x => x.EloDelta7Days)

            .FirstOrDefault();



    var eloDonator =

        playersWithHistory

            .OrderBy(

                x => x.EloDelta7Days)

            .FirstOrDefault();



    var mostActive =

        players

            .OrderByDescending(

                x => x.ActivityMatches)

            .First();



    var leastActive =

        players

            .OrderBy(

                x => x.ActivityMatches)

            .First();



    AppendAward(

        sb,

        "👑",

        "ELO-KUNGEN",

        $"{eloKing.Name} — {eloKing.Elo} ELO");



    AppendAward(

        sb,

        "⚔️",

        "K/D-DEMONEN",

        $"{bestKd.Name} — {bestKd.Kd:0.00} K/D");



    AppendAward(

        sb,

        "📈",

        "VINSTMASKINEN",

        $"{bestWinRate.Name} — " +

        $"{bestWinRate.WinRate:0}% vinst");



    AppendAward(

        sb,

        "🎯",

        "AIM-KUNGEN",

        $"{aimKing.Name} — " +

        $"{aimKing.HeadshotPercentage:0}% HS");



    AppendAward(

        sb,

        "💣",

        "FRAGMASKINEN",

        $"{fragMachine.Name} — " +

        $"{fragMachine.AverageKills:0.0} kills/match");



    AppendAward(

        sb,

        "💥",

        "SKADEMASKINEN",

        $"{damageDealer.Name} — " +

        $"{damageDealer.Adr:0.0} ADR");



    AppendAward(

        sb,

        "⭐",

        "MVP-BONDEN",

        $"{mvpFarmer.Name} — " +

        $"{mvpFarmer.TotalMvps} MVP");



    if (stonks is not null &&

        stonks.EloDelta7Days > 0)

    {

        AppendAward(

            sb,

            "🚀",

            "STONKS",

            $"{stonks.Name} — " +

            $"+{stonks.EloDelta7Days} ELO");

    }



    if (eloDonator is not null &&

        eloDonator.EloDelta7Days < 0)

    {

        AppendAward(

            sb,

            "📉",

            "ELO-DONATORN",

            $"{eloDonator.Name} — " +

            $"{eloDonator.EloDelta7Days} ELO");

    }



    if (hottest is not null)

    {

        AppendAward(

            sb,

            "🔥",

            "GLÖDHET",

            $"{hottest.Name} — " +

            $"{hottest.Streak} raka vinster");

    }



    AppendAward(

        sb,

        "🦟",

        "MYGGBETTET",

        $"{lowestAdr.Name} — " +

        $"{lowestAdr.Adr:0.0} ADR");



    AppendAward(

        sb,

        "💀",

        "DONATIONEN",

        $"{walkingDonation.Name} — " +

        $"{walkingDonation.Kd:0.00} K/D");



    AppendAward(

        sb,

        "🙈",

        "SIKTESFÖRBUD",

        $"{lowestHs.Name} — " +

        $"{lowestHs.HeadshotPercentage:0}% HS");



    AppendAward(

        sb,

        "😴",

        "MVP-ALLERGI",

        $"{lowestMvps.Name} — " +

        $"{lowestMvps.TotalMvps} MVP");



    AppendAward(

        sb,

        "🚨",

        "FORMKRIS",

        $"{lowestWinRate.Name} — " +

        $"{lowestWinRate.WinRate:0}% vinst");



    AppendAward(

        sb,

        "🚓",

        "UNDER UTREDNING",

        $"{lowestElo.Name} — " +

        $"{lowestElo.Elo} ELO");



    AppendAward(

        sb,

        "🎮",

        "ARBETSLÖSA KRIGAREN",

        $"{mostActive.Name} — " +

        $"{mostActive.ActivityMatches} matcher senaste {activityDays} dagarna");



    AppendAward(

        sb,

        "🛋️",

        "SOFFGENERALEN",

        $"{leastActive.Name} — " +

        $"{leastActive.ActivityMatches} matcher senaste {activityDays} dagarna");



    sb.AppendLine(

        "*Prestationsstatistik baserad på de senaste " +

        "10 FACEIT-matcherna.*");



    sb.AppendLine(

        $"*Aktivitet baserad på matcher de senaste {activityDays} dagarna.*");



    sb.AppendLine();



    sb.AppendLine(

        $"🕐 Uppdaterad " +

        $"<t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>");



    return sb.ToString();

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

        $"*Kartstatistiken använder upp till " +

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



internal sealed class CalculatedStats

{

    public int Matches { get; init; }



    public int Wins { get; init; }



    public int Losses { get; init; }



    public int TotalKills { get; init; }



    public int TotalMvps { get; init; }



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



    public int TripleKills { get; init; }



    public int QuadroKills { get; init; }



    public int PentaKills { get; init; }

}