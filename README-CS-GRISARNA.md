# CS Grisarna – FACEIT-tavlan

En liten .NET-app som hämtar FACEIT-statistik för gänget och håller Discord uppdaterad med ranking, utmärkelser, kartor och tilt.

Tanken är enkel: statistiken ska vara kul att läsa, men ändå bygga på riktiga siffror.

## Vad som visas i Discord

Appen håller fyra fasta meddelanden uppdaterade:

1. **🏅 Grisarnas Utmärkelser**  
   Roliga awards för allt från ELO och K/D till entrydueller, clutch och aktivitet.

2. **🐷 Grisarnas Ranking**  
   Gruppen sorterad på ELO, med form och statistik från gruppmatcher.

3. **🗺️ Kartanalys**  
   Hur gruppen presterar på olika kartor.

4. **🧊 Tiltövervakning**  
   Håller koll på när någon börjar stapla förluster.

Meddelandena uppdateras i stället för att nya skapas varje gång.

---

## Vad räknas som en gruppmatch?

Vi vill inte att en massa soloqueue ska påverka gruppens statistik.

En match räknas därför som en **gruppmatch** när minst **3 spelare från `FACEIT_PLAYERS`** finns i samma lag och samma FACEIT-match.

Rankingen tittar på spelarens **15 senaste FACEIT-matcher** och använder bara de matcher som klarar gruppfiltret.

- Minst **2 gruppmatcher av 15** krävs för att synas i rankingen.
- Minst **5 gruppmatcher** krävs för att kunna få en utmärkelse.

Spelare som inte klarar gränsen visas längst ner som **Ej med**, så det är tydligt varför någon saknas.

---

## Grisarnas Ranking

Varje spelare får en kompakt rad med de viktigaste siffrorna:

- 📊 **V-F** – vinster och förluster i gruppmatcherna
- ⚔️ **K/D** – kills / deaths
- 💥 **ADR** – average damage per round
- 🎯 **HS** – headshot %
- 🚪 **Entry kills/m** – vunna entrydueller per match
- ⚡ **Entrydueller/m** – hur många entrydueller spelaren tar per match
- ✅ **Entry-vinst%** – hur stor andel av entryduellerna som vinns
- 🧠 **Clutch** – vinstprocent i 1v1 och 1v2
- 🆚 **Motst. snitt-ELO** – motståndarnas nuvarande genomsnittliga FACEIT ELO i de kvalificerade gruppmatcherna

Advanced-statistiken kommer direkt från FACEIT:s matchstats.

---

## 🎭 Spelstil / Grisindex

Grisindexet försöker beskriva **hur framåt eller försiktigt någon spelar**. Det är inte ett betyg på hur bra spelaren är.

Skalan går ungefär från:

- 🦍 **5–25** – först in
- 🔥 **26–45** – aggressiv
- ⚖️ **46–65** – balanserad
- 🐢 **66–80** – försiktig
- 🐔 **81–95** – spelar mer bakåt

Indexet bygger på två saker:

**75 % hur ofta spelaren tar en entryduell**  
**25 % hur många entrydueller spelaren tar per match**

I koden normaliseras ungefär:

- Entry rate: **8 % = passivt**, **30 % = aggressivt**
- Entrydueller/match: **1.5 = passivt**, **6.0 = aggressivt**

Sedan vänds värdet så att:

> **Låg siffra = mer gas. Hög siffra = mer bakåt.**

Indexet begränsas till **5–95**, så ingen blir automatiskt 0 eller 100 bara för att vara mest extrem i gruppen.

Viktigt: **entry-vinstprocenten påverkar inte Grisindexet.**  
Någon kan springa först varje runda och förlora alla dueller – aggressiv spelstil, kanske inte alltid en bra idé.

---

## 🏅 Grisarnas Utmärkelser

Utmärkelserna är en blandning av seriös statistik och intern grislogik.

Exempel:

- 👑 ELO-KUNGEN
- ⚔️ K/D-DEMONEN
- 📈 VINSTMASKINEN
- 🎯 AIM-KUNGEN
- 💣 FRAGMASKINEN
- 💥 SKADEMASKINEN
- ⭐ MVP-BONDEN
- 🦟 MYGGBETTET
- 🚨 FORMKRIS
- 🎮 ARBETSLÖSA KRIGAREN
- 🛋️ SOFFGENERALEN
- 🦍 FÖRST IN SIST UT
- 🚪 DÖRRSPARKAREN
- 🎯 ENTRY-DUELLKUNGEN
- 🧠 CLUTCHKUNGEN
- 💣 SPRÄNGMÄSTAREN
- 💡 BLÄNDVERKET
- 🐔 BAKRADSOPERATÖREN

För utmärkelser krävs minst **5 gruppmatcher**, så någon inte vinner en kategori på ett väldigt litet sample.

---

## Kartanalys

Kartstatistiken använder upp till de **50 senaste matcherna per spelare**, men även här räknas bara matcher som kvalificerar sig som gruppmatcher.

Det ger bland annat en bild av:

- vilka kartor gruppen spelar mest
- winrate per karta
- ADR och HS per karta
- bästa och sämsta kartan

---

## Aktivitet och ELO-historik

Aktivitet räknas över de senaste **30 dagarna** och används bland annat för:

- 🎮 Arbetslösa krigaren
- 🛋️ Soffgeneralen

ELO sparas i:

```text
history/elo-history.json
```

Det gör att tavlan med tiden kan visa förändring i ELO i stället för bara aktuell nivå.

---

## GitHub-inställningar

### Secrets

```text
FACEIT_API_KEY
DISCORD_WEBHOOK_URL
MONTHLY_DISCORD_WEBHOOK_URL
```

### Repository variables

```text
FACEIT_PLAYERS
AWARDS_MESSAGE_ID
DISCORD_MESSAGE_ID
MAP_STATS_MESSAGE_ID
TILT_WATCH_MESSAGE_ID
```

`FACEIT_PLAYERS` är en kommaseparerad lista med FACEIT-nick.

Exempel:

```text
chrillebille,BOSSEN-_-,ibbann,raveleif
```

Message-ID:n gör att appen uppdaterar samma Discord-meddelanden varje gång.

---

## GitHub Actions

Workflowen finns i:

```text
.github/workflows/faceit-board.yml
```

Den kan köras manuellt med `workflow_dispatch` och är även schemalagd att köras varje timme.

Vid varje körning:

1. Projektet byggs.
2. FACEIT-data hämtas.
3. Gruppmatcher identifieras.
4. Advanced stats hämtas för gruppmatcherna.
5. Discord-meddelandena uppdateras.
6. Ny ELO-historik sparas tillbaka till repot.

---

## Teknik

Projektet är medvetet ganska enkelt:

```text
.NET 8
FACEIT Data API
Discord Webhook
GitHub Actions
JSON-fil för ELO-historik
```

Ingen databas och ingen tung infrastruktur. GitHub Action kör jobbet, FACEIT står för datan och Discord visar resultatet.

---

## Grundtanken

Det här är inte tänkt som en seriös esportplattform.

Det är **CS Grisarnas interna statistikcentral**.

Siffrorna ska stämma, men presentationen får gärna vara lite dum.

---

## 🏆 Månadens spelare

Månadens spelare ligger i en egen Discord-kanal och använder `MONTHLY_DISCORD_WEBHOOK_URL`.

Det är ett månadspris, inte en live-tavla. När en kalendermånad är slut räknar appen på **alla gruppmatcher från den avslutade månaden**. Minst **30 gruppmatcher** krävs för att kunna vinna.

Ratingen är 0–100 och består av **80 baspoäng + upp till 20 bonuspoäng**:

- **35 poäng ELO-klättring under månaden**
- 20 poäng ADR
- 15 poäng winrate
- 10 poäng entry-impact
- **Upp till 20 poäng underdog-bonus** för bra spel mot starkare motstånd

**K/D ingår inte alls i poängräkningen.** K/D visas fortfarande som en vanlig statistikrad, men används varken i grundpoängen eller för att kvalificera sig till underdog-bonusen.

ELO-formen ger 0 poäng vid -100 ELO och maximal utdelning vid +150 ELO. Vid saknad historik används neutral ELO-poäng (40 % av ELO-delen). Hög nuvarande ELO ger **inte** poäng i sig.

Underdog-bonus räknas per kvalificerad gruppmatch när det finns motståndar-ELO. För att en match ska räknas krävs att **motståndarlagets snitt ligger minst 100 ELO över spelarens uppskattade månads-ELO**, samtidigt som spelaren når **minst 75 ADR och minst 90 % av sin egen månads-ADR** i den matchen. Starkare motstånd (upp till +400 ELO i poängmodellen), högre ADR och större överprestation jämfört med den egna nivån ger mer bonus. Antalet kvalificerade prestationer skalar bonusen upp till full möjlig effekt vid åtta matcher, så en enstaka lyckträff inte dominerar månaden. Bonusen visas både i liveprognosen och på vinnartavlan.

**Begränsning:** FACEIT-uppslaget ger motståndarnas *aktuella* ELO, inte deras ELO när matchen spelades. Egen ELO approximeras som mittvärdet mellan månadens start- och slutvärde. Matchup-bonusen är därför en uppskattning. Om motståndar-ELO inte kan hämtas delas inga bonuspoäng ut för den matchen. ELO-förändringen är kontots förändring, inte bara ELO från gruppmatcherna.

Entry-impact består av 70 % entry success och 30 % entry kills per match. MVP/match visas på vinnaren men påverkar inte ratingen.

### Bara ett meddelande per månad

Appen sparar publicerade månader i:

```text
history/monthly-player-state.json
```

Första gången funktionen körs sparas månaden då spårningen startade. Den publicerar alltså inte gamla månader bakåt i tiden. När nästa månad börjar tittar den på föregående, avslutade månad.

Efter att Discord-meddelandet har skapats läggs exempelvis `2026-10` i `PublishedMonths`. På alla senare körningar ser appen att oktober redan är publicerad och hoppar över den. Workflowen committar state-filen tillsammans med ELO-historiken, så skyddet finns kvar mellan GitHub Actions-körningar.

Det behövs ingen `MONTHLY_PLAYER_MESSAGE_ID`, eftersom varje månadspris ska ligga kvar som ett eget historiskt meddelande i kanalen.

Om Discord-anropet misslyckas markeras månaden inte som publicerad, så nästa körning kan försöka igen.



## FACEIT rate limit och motståndar-ELO

Motståndar-ELO cachas i `history/opponent-elo-cache.json` i upp till 24 timmar för att minska antalet FACEIT-anrop. Advanced stats och ELO-uppslag använder även retry/backoff vid 429/5xx. Om advanced-data saknas för någon match visas `⚠️ x/y adv`; varningen försvinner automatiskt när alla relevanta matcher har analyserats.
