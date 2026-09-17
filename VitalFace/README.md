# VitalFace Station — Rapporto Tecnico e Implementazione

Totem web di pre-triage contactless: in ~30 secondi stima frequenza cardiaca (rPPG/CHROM),
frequenza respiratoria (movimento spalle), affaticamento/PERCLOS (blink rate) — zero contatto,
zero video registrato. Target: farmacie, medicina del lavoro, RSA, logistica.

Questo repository contiene l'implementazione funzionante dell'architettura descritta nel brief,
non solo il documento di design. Tutto il codice qui sotto compila, i test passano e la pipeline
CHROM/PERCLOS è stata validata end-to-end con payload sintetici (vedi "Cosa è stato verificato").

## Struttura della soluzione

```
VitalFace/
  VitalFace.slnx                       # .NET 10 usa il nuovo formato .slnx al posto di .sln
  docker-compose.yml
  src/
    VitalFace.Core/                    # Domain + algoritmi, nessuna dipendenza da framework/DB
      Models/                          # VitalSnapshot, Screening, ConsentRecord, VitalResult, ...
      Abstractions/                    # IRppgProcessor, IFatigueDetector, IScreeningRepository, ...
      Services/
        RppgProcessor.cs               # CHROM (de Haan & Jeanne, 2013)
        FatigueDetector.cs             # PERCLOS
        RespiratoryRateEstimator.cs    # FFT su movimento verticale spalle
        SignalProcessing/DigitalSignalProcessing.cs  # detrend, window, FFT, ricerca picco
    VitalFace.Contracts/                # DTO condivisi tra Api e Web (evita drift client/server)
    VitalFace.Infrastructure/           # EF Core 10 + Npgsql, QuestPDF, migrations
    VitalFace.Api/                      # Minimal API
    VitalFace.Web/                      # Blazor WebAssembly Standalone (kiosk, PWA)
      wwwroot/js/kiosk/                 # face-bridge.js (MediaPipe), p5-rppg-shader.js,
                                        # pulse-viz.js, signature-pad.js, kiosk-interop.js
  tests/
    VitalFace.Core.Tests/               # 22 test xUnit sugli algoritmi (CHROM, PERCLOS, DSP)
```

## Come eseguire in locale

```bash
# Prerequisiti: .NET 10 SDK, PostgreSQL raggiungibile (o docker compose, vedi sotto)

# 1. Applica le migrazioni e avvia l'API (applica le migration automaticamente in Development)
cd src/VitalFace.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run

# 2. In un altro terminale, avvia il kiosk Blazor
cd src/VitalFace.Web
dotnet run
```

Oppure con Docker:

```bash
echo "VITALFACE_DB_PASSWORD=change-me" > .env
docker compose up --build
```

Nota: `docker-compose.yml` non applica le migration EF Core automaticamente in produzione (solo in
`Development`, vedi `VitalFace.Api/Program.cs`) — è una scelta deliberata: applicare schema in
automatico all'avvio di un container di produzione è un rischio (race condition su repliche
multiple, downtime non controllato). Il release process reale deve eseguire
`dotnet ef database update` come step esplicito prima del rollout.

### Esporre il kiosk pubblicamente con un Cloudflare Quick Tunnel

`scripts/run-with-cloudflare-tunnel.sh` clona (o aggiorna) questo branch, builda e avvia
`docker compose`, poi apre due Cloudflare Quick Tunnel (nessun account Cloudflare richiesto): uno
per l'API e uno per il kiosk. Va eseguito su una macchina con accesso reale a Internet — un
sandbox CI/agent con un proxy in uscita restrittivo tipicamente blocca sia il download del binario
`cloudflared` sia il protocollo del tunnel stesso.

```bash
curl -fsSL https://raw.githubusercontent.com/pablozramirez73/claude-research-pipeline/claude/vitalface-station-app-s60ij3/VitalFace/scripts/run-with-cloudflare-tunnel.sh | bash
# oppure, se hai già clonato il branch:
./VitalFace/scripts/run-with-cloudflare-tunnel.sh
```

Su Windows è disponibile lo stesso script in PowerShell 7+ (`scripts/run-with-cloudflare-tunnel.ps1`),
con la stessa logica (clone/aggiornamento del branch, build, i due tunnel, il riavvio incrociato
di api/kiosk):

```powershell
irm https://raw.githubusercontent.com/pablozramirez73/claude-research-pipeline/claude/vitalface-station-app-s60ij3/VitalFace/scripts/run-with-cloudflare-tunnel.ps1 | iex
# oppure, se hai già clonato il branch:
.\VitalFace\scripts\run-with-cloudflare-tunnel.ps1
```

Richiede Docker Desktop e PowerShell 7+ (`winget install Microsoft.PowerShell`); installa
`cloudflared` da solo via `winget` o, in mancanza, scaricando il binario ufficiale.

Perché due tunnel e non uno: un Cloudflare Quick Tunnel espone un solo servizio locale per
hostname pubblico, quindi API e kiosk finiscono su due hostname `*.trycloudflare.com` diversi.
Lo script gestisce la dipendenza incrociata: avvia prima l'API, apre il suo tunnel, poi
(ri)crea il container del kiosk iniettando quell'URL come `API_BASE_URL` (rigenerato a runtime da
un entrypoint `envsubst`, vedi `VitalFace.Web/docker/docker-entrypoint.sh` — nessun rebuild
dell'immagine necessario), apre il tunnel del kiosk, e infine riavvia l'API con il CORS
(`Cors:KioskOrigins`) aggiornato all'origine pubblica del kiosk. Gli URL sono temporanei e HTTPS
(necessario per l'accesso alla webcam via `getUserMedia`), e cambiano ad ogni riavvio dello script.

## Cosa è stato verificato in questa sessione

Ambiente sandbox senza accesso alle CDN Microsoft/jsdelivr (policy di rete dell'agente), quindi la
verifica è stata fatta a più livelli, tutti realmente eseguiti (non simulati):

- **`dotnet build` sull'intera solution**: 0 warning, 0 errori, tutti i 6 progetti.
- **`dotnet test`**: 22/22 test passati, incluso il recupero di una frequenza cardiaca nota
  (60/75/100 bpm) da un segnale RGB sintetico via CHROM, con tolleranza ±3 bpm dettata dalla
  risoluzione FFT reale, e il recupero di una frequenza respiratoria nota (12/16/24 atti/min).
- **API end-to-end reale**: PostgreSQL 16 installato e avviato nel sandbox, `dotnet run` sull'Api,
  richieste HTTP reali a `/api/vitals/analyze` con payload sintetico → CHROM ha stimato **75.6 bpm**
  contro un target di 75, e **15.8 atti/min** contro un target di 16; un secondo payload con
  tachicardia simulata (140 bpm) e occhi chiusi ha correttamente prodotto `isCritical: true` con le
  motivazioni testuali corrette. `GET /api/vitals/{id}/ticket` ha restituito un PDF valido
  (verificato con `file`, 1 pagina). `/health` → `Healthy`.
- **Blazor WASM kiosk**: build Release, avviato con `dotnet run`, caricato con Playwright/Chromium
  reale — la Idle screen renderizza correttamente (titolo, testo, CSS kiosk applicato). Gli unici
  errori in console sono il blocco CDN del sandbox verso `cdn.jsdelivr.net` (p5.js/MediaPipe) e un
  404 di favicon, entrambi non riproducibili su una rete kiosk reale.
- **Non verificato in questa sessione** (richiede una rete reale + webcam + touch screen): il
  flusso completo firma→countdown→cattura→MediaPipe→shader p5.js in un browser con accesso
  Internet vero. La logica JS è stata scritta per essere realmente eseguibile (API MediaPipe Tasks
  Vision e p5.js 2.x corrette), ma il test di accettazione finale va fatto sull'hardware target.

## Note tecniche e scostamenti dal brief originale

Il brief chiedeva esplicitamente ".NET 11" e "Native AOT compatible". Due chiarimenti doverosi:

1. **.NET 11 non esiste ancora** (knowledge cutoff: gennaio 2026; data odierna: settembre 2026).
   L'ultima LTS disponibile è **.NET 10** (rilasciata nov. 2025), usata qui in tutti i progetti.
   Il codice non usa API deprecate: il porting a .NET 11 quando disponibile dovrebbe limitarsi a
   un bump di `TargetFramework` e al retest delle dipendenze (EF Core, Npgsql, QuestPDF).
2. **Native AOT non è stato abilitato**, e non lo consiglierei nella forma attuale: EF Core+Npgsql
   e QuestPDF (che usa SkiaSharp e un layout engine basato su reflection) non hanno oggi supporto
   Native AOT maturo e testato in produzione insieme. Dichiarare "AOT compatible" nel brief è
   un obiettivo aspirazionale più che uno stato attuale dell'ecosistema. Per un vero percorso AOT
   servirebbe: (a) sostituire QuestPDF con un renderer PDF AOT-friendly o spostare la generazione
   ticket in un servizio separato non-AOT, (b) usare EF Core Compiled Models, (c) validare Npgsql
   con trimming abilitato. Ho preferito un'API "normale" solida e testata piuttosto che un'AOT
   che si romperebbe silenziosamente in produzione.

Altri limiti espliciti dello scaffold, tutti scelte deliberate e documentate (non omissioni):

- **Modelli MediaPipe (`.task`)** non inclusi nel repository (binari di alcuni MB, licenza Google):
  vanno scaricati e posizionati in `VitalFace.Web/wwwroot/models/face_landmarker.task` e
  `pose_landmarker_lite.task` prima del deploy. Il service worker li cache già per l'uso offline
  (`\.task$` incluso in `offlineAssetsInclude`).
- **Indici landmark ROI** (fronte/guance in `face-bridge.js`) sono un punto di partenza da
  letteratura rPPG, non calibrati su un dataset reale — vanno validati con la fotocamera e la
  distanza operativa del totem specifico prima di un uso clinico-adiacente.
- **Soglie cliniche** (`ClinicalThresholds` in Core) sono bande conservative di "segnala per
  revisione umana", non soglie diagnostiche, e sono volutamente configurabili a runtime (property
  statiche, non costanti) perché una farmacia e un magazzino di logistica vogliono probabilmente
  bande diverse.
- **Orchestrazione fleet con .NET Aspire** e **OTA dei modelli** menzionate nel brief non sono
  implementate in questo scaffold: sono investimenti operativi (multi-totem, telemetria
  centralizzata) che vanno pianificati quando c'è più di un totem reale da gestire, non prima.

## Sicurezza e Compliance (implementato)

- **Nessun video/frame viene mai inviato al backend**: il payload di `/api/vitals/analyze`
  contiene solo array di `float` (colore medio ROI, apertura occhi, posizione spalle) — vedi
  `VitalSnapshot.Validate()` e `VitalAnalysisRequest`. È impossibile ricostruire un volto da questi
  dati.
- **Consenso GDPR**: la firma touch (canvas p5.js) viene hashata con SHA-256 lato server
  (`ConsentRecord.FromSignatureBytes`) e solo l'hash viene persistito — l'immagine della firma non
  è mai salvata su disco.
- **CORS** ristretto via configurazione (`Cors:KioskOrigins`), nessun'origine wildcard di default.
- **Dominio persistence-ignorant**: `Screening` (Core) non ha alcuna dipendenza da EF Core; il
  mapping è tutto in `ScreeningConfiguration` (Infrastructure), quindi il dominio resta testabile
  in isolamento e sostituibile (es. passaggio futuro a un altro ORM) senza toccare la logica.

## Rischi e Best Practice (produzione)

| Area | Rischio | Mitigazione consigliata |
|---|---|---|
| Accuratezza clinica | rPPG e PERCLOS sono stime, sensibili a movimento/illuminazione | Il disclaimer "pre-triage, non diagnosi" è già in UI e nel PDF; **non rimuoverlo** in nessuna vetrina commerciale |
| Modelli MediaPipe | `.task` files grandi, licenza Google, aggiornamenti nel tempo | Pinnare la versione, verificare hash all'avvio, pipeline di aggiornamento controllata (non "latest" da CDN in produzione) |
| CORS/Origini | Il totem parla con l'API su rete pubblica/negozio | Fissare `Cors:KioskOrigins` per ambiente, mai wildcard in produzione |
| Connessione DB | `EnableRetryOnFailure` già configurato, ma nessun circuit breaker applicativo | Aggiungere Polly se il volume di totem cresce oltre poche decine |
| Migrazioni EF Core | Auto-migrate abilitato solo in `Development` | Mantenere così; introdurre un job di migrazione esplicito nel pipeline CI/CD |
| Native AOT | Non abilitato (vedi sopra) | Non forzarlo finché QuestPDF/EF Core non hanno supporto maturo e testato |
| Sicurezza consenso | L'hash prova che *una* firma è stata raccolta, non impedisce input vuoti banali | Aggiungere una soglia minima di "inchiostro" (già presente lato client: `hasSignature()`), eventualmente firma con timestamp lato server già presente in `ConsentRecord.ConsentedAtUtc` |

## Come proseguire

1. Scaricare i modelli `.task` ufficiali MediaPipe e posizionarli in `wwwroot/models/`.
2. Calibrare gli indici ROI (`face-bridge.js`) e i fattori di amplificazione shader sulla
   fotocamera/hardware kiosk reale.
3. Test di accettazione end-to-end su hardware kiosk reale (Chrome kiosk mode, tablet/PC con
   webcam), non solo build/unit test.
4. Valutare .NET Aspire per l'orchestrazione multi-totem solo quando serve gestire più di un
   dispositivo in produzione.
