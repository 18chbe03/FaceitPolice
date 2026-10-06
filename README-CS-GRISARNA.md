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

Rankingen tittar på spelarens **10 senaste FACEIT-matcher** och använder bara de matcher som klarar gruppfiltret.

- Minst **2 gruppmatcher av 10** krävs för att synas i rankingen.
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
