# KineMotion Rehab — Rapporto Tecnico e Implementazione

Piattaforma di telereabilitazione gamificata: il paziente gioca con il corpo (webcam + MediaPipe),
il sistema misura ROM, fluidità del movimento, compenso posturale e tremore con logica clinica
reale, non solo un tracker di movimento generico. Target: centri fisioterapici, cliniche
neurologiche, assicurazioni.

Questo repository contiene l'implementazione funzionante dell'architettura descritta nel brief,
non solo il documento di design. Tutto il codice qui sotto compila, i test passano e la pipeline
ROM/tremore/compenso è stata validata end-to-end con payload sintetici *e* con una vera istanza
PostgreSQL e chiamate HTTP/SignalR reali (vedi "Cosa è stato verificato in questa sessione").

## Struttura della soluzione

```
KineMotion/
  KineMotion.slnx                      # .NET 10 usa il nuovo formato .slnx al posto di .sln
  docker-compose.yml
  src/
    KineMotion.Core/                   # Domain + algoritmi, nessuna dipendenza da framework/DB
      Entities/                        # Patient, Prescription, Session
      ValueObjects/                    # PoseFrame, ExerciseType, JointAngle
      Models/                          # RomResult, TremorResult
      Services/
        RomCalculator.cs               # ROM, normalized-jerk smoothness, compenso del tronco
        TremorAnalyzer.cs              # FFT su posizione polso, tremore 3-12 Hz
        SignalProcessing/Fft.cs        # detrend, window, Cooley-Tukey FFT (nessuna dipendenza nativa)
    KineMotion.Contracts/              # DTO condivisi tra Api e Web (evita drift client/server)
    KineMotion.Infrastructure/         # EF Core 10 + Npgsql, migrations, repository
    KineMotion.Api/                    # Minimal API + SignalR Hub
      Hubs/RehabHub.cs                 # Live view: StreamMetrics -> gruppo per fisioterapista
      Endpoints/                       # Prescriptions, Sessions
      Exercises/ExerciseAnalysisPlan.cs
    KineMotion.Web/                    # Blazor WebAssembly Standalone (paziente)
      Pages/Play/ShoulderBird.razor
      wwwroot/js/mediapipe/pose-game-bridge.js   # MediaPipe Tasks Vision PoseLandmarker
      wwwroot/js/games/p5-bird.js                # p5.js 1.x game engine
  tests/
    KineMotion.Core.Tests/             # 13 test xUnit sugli algoritmi (ROM, jerk, tremore FFT)
```

## Come eseguire in locale

```bash
# Prerequisiti: .NET 10 SDK, PostgreSQL raggiungibile (o docker compose, vedi sotto)

# 1. Applica le migrazioni e avvia l'Api (applica le migration automaticamente in Development)
cd src/KineMotion.Api
ASPNETCORE_ENVIRONMENT=Development dotnet run

# 2. In un altro terminale, avvia il client Blazor del paziente
cd src/KineMotion.Web
dotnet run
```

Oppure con Docker:

```bash
echo "KINEMOTION_DB_PASSWORD=change-me" > .env
docker compose up --build
```

Nota: `docker-compose.yml` non applica le migration EF Core automaticamente in produzione (solo in
`Development`, vedi `KineMotion.Api/Program.cs`) — è una scelta deliberata: applicare schema in
automatico all'avvio di un container di produzione è un rischio (race condition su repliche
multiple, downtime non controllato). Il release process reale deve eseguire
`dotnet ef database update` come step esplicito prima del rollout.

Prima del deploy, scaricare `pose_landmarker_lite.task` (vedi
`src/KineMotion.Web/wwwroot/models/README.md`) — non incluso nel repository perché è un binario
multi-MB con licenza Google.

## Cosa è stato verificato in questa sessione

A differenza della sessione precedente su questo stesso pattern (VitalFace Station), l'ambiente
sandbox di **questa** sessione aveva accesso a `apt.archive.ubuntu.com` (da cui è stato installato
davvero il .NET 10 SDK via `apt-get install dotnet-sdk-10.0`) e a una PostgreSQL 16 locale reale —
quindi la verifica qui è stata end-to-end, non solo build/unit test:

- **`dotnet build` sull'intera solution** (`KineMotion.slnx`): 0 warning, 0 errori, tutti i 6
  progetti (Core, Contracts, Infrastructure, Api, Web, Core.Tests).
- **`dotnet test`**: 13/13 test passati, incluso il recupero di un ROM noto (125°) da una traiettoria
  sintetica a minimum-jerk, la verifica che un movimento fluido (5°-polinomio) ottenga uno
  `SmoothnessScore` più basso di uno rumoroso con gli stessi estremi, la corretta rilevazione di
  compenso del tronco sopra/sotto soglia, e il recupero di frequenze di tremore note (4.5/5/8 Hz)
  da un segnale sintetico via FFT, con un falso-negativo corretto per un'oscillazione volontaria a
  1 Hz (fuori dalla banda patologica 3-12 Hz).
- **Migrazione EF Core reale**: `dotnet ef migrations add InitialCreate` generata e applicata con
  `dotnet ef database update` equivalente (auto-migrate in Development) contro PostgreSQL 16
  installato nel sandbox (`apt-get install postgresql-16`) — tabelle `patients`, `prescriptions`,
  `sessions` create correttamente, con `Frames` come colonna JSON (`OwnsMany(...).ToJson()`).
- **Api end-to-end reale**: `dotnet run` sull'Api, richieste HTTP reali via `curl`/Python:
  - `POST /api/prescriptions` → creata una prescrizione ShoulderBirdFlight (10 rep, target 120°),
    persistita e rileggibile via `GET /api/prescriptions/{id}`.
  - `POST /api/sessions/complete` con una traiettoria sintetica a minimum-jerk (5°→125° in 1.5s,
    30fps) → ROM massimo recuperato **124.99°** (target 125°), `CompensationDetected: false`.
  - Stessa chiamata con una prescrizione MirrorHand e un segnale sintetico di tremore a 5 Hz
    (30fps, 3.4s) → `TremorDominantFrequencyHz: 4.92`, `TremorIndex: 0.989`, `TremorPresent: true`.
  - Stessa chiamata con `TrunkLeanDeg: 25` costante → `CompensationDetected: true`,
    `CompensationRatio: 1.0`.
  - `GET /api/sessions/{id}/summary` → ricalcola dai frame persistiti, risultato identico a quello
    restituito da `complete` (conferma che il round-trip JSON-in-DB non perde precisione).
  - `GET /api/sessions/{id-inesistente}/summary` → `404`.
  - Un bug reale è stato trovato e corretto in questa fase: senza un `JsonStringEnumConverter`
    esplicito su `ExerciseTypeDto`, System.Text.Json si aspettava un intero ordinale invece della
    stringa `"ShoulderBirdFlight"` e falliva con 500 — vedi `KineMotion.Contracts/ExerciseTypeDto.cs`.
- **SignalR reale**: due `HubConnection` .NET separati (uno che simula il fisioterapista, uno il
  paziente) contro l'Api in esecuzione — il fisioterapista chiama `JoinTherapistGroup`, il paziente
  chiama `StreamMetrics`, e la connessione del fisioterapista **riceve davvero** l'evento
  `MetricUpdate` col payload corretto (non simulato: due processi, un vero handshake WebSocket).
- **Blazor WASM con Playwright/Chromium reale** (stesso binario pre-installato nel sandbox, con
  `--use-fake-device-for-media-stream` per simulare una webcam): la Home page carica, il bottone
  "Avvia demo" chiama davvero `POST /api/prescriptions` sull'Api in esecuzione (bloccato da CORS
  finché l'origine del dev server Blazor non è stata aggiunta a `Cors:PatientOrigins` — comportamento
  corretto, non un bug), naviga a `/play/shoulder-bird/{id}`, la pagina carica la prescrizione via
  `GET /api/prescriptions/{id}` e mostra "ShoulderBirdFlight" con gli obiettivi corretti. Cliccando
  "Avvia webcam e gioco" il fallback di errore JS funziona correttamente: la webcam finta si avvia,
  ma il modulo MediaPipe Tasks Vision non può essere scaricato (CDN bloccata dalla policy di rete
  del sandbox, vedi sotto) e l'interfaccia mostra il messaggio d'errore italiano previsto invece di
  un crash silenzioso.
- **Superficie API MediaPipe verificata contro il codice sorgente reale**, non solo a memoria: con
  accesso a un clone di google/mediapipe, `pose_landmarker.ts` e `fileset_resolver.ts.template`
  confermano che `FilesetResolver.forVisionTasks(basePath)`, le opzioni
  `numPoses`/`minPoseDetectionConfidence`/`minPosePresenceConfidence`/`minTrackingConfidence`, la
  firma `detectForVideo(videoFrame, timestampMs)` (sincrona, senza callback, ritorna
  `PoseLandmarkerResult` quando il running mode è `VIDEO`) e la forma del risultato
  (`result.landmarks: NormalizedLandmark[][]`, un array di pose ciascuna con landmark
  `{x, y, z}` normalizzati) usate in `pose-game-bridge.js` corrispondono esattamente all'API reale.
- **Non verificato in questa sessione** (richiede una rete reale + webcam vera): il flusso completo
  cattura-webcam → `PoseLandmarker.detectForVideo` → calcolo angoli → rendering p5.js del gioco.
  `cdn.jsdelivr.net` (da cui si caricano sia `@mediapipe/tasks-vision` che `p5.js`) è bloccato dalla
  policy di rete di questo ambiente agente (`connect_rejected` sul proxy), quindi l'API è stata
  verificata contro il sorgente ma non eseguita realmente end-to-end con un modello `.task` vero.
  Gli indici landmark BlazePose a 33 punti usati per gli angoli (spalla/gomito/tronco) sono la
  topologia standard pubblicata da Google, ma vanno comunque accettati su hardware reale con
  webcam vera prima del rilascio.

## Note tecniche e scostamenti dal brief originale

Il brief chiedeva esplicitamente ".NET 11". Come per il precedente scaffold VitalFace Station sullo
stesso repository: **.NET 11 non esiste ancora** (knowledge cutoff: gennaio 2026; data odierna:
settembre 2026). L'ultima LTS disponibile è **.NET 10**, usata qui in tutti i progetti. Il codice
non usa API deprecate: il porting a .NET 11 dovrebbe limitarsi a un bump di `TargetFramework`.

Scostamenti *di scope*, tutti scelte deliberate e documentate (non omissioni):

- **Solo 1 dei 5 giochi è implementato end-to-end** (ShoulderBirdFlight — abduzione spalla). Gli
  altri quattro (FruitPickReach, BalanceBoard, MirrorHand, BreathFlower) hanno il loro percorso
  dati lato server pronto (`ExerciseType` enum, `ExerciseAnalysisPlan` che sceglie quale analizzatore
  applicare — vedi sotto), ma non un p5.js sketch dedicato. Cinque giochi completi con collisioni,
  particellari e tuning del game-feel è un investimento di design/QA, non solo di codice, e andrebbe
  fatto con un fisioterapista che validi il mapping movimento→gioco su pazienti reali, non
  indovinato in una sessione di scaffold.
- **BalanceBoard usa il tronco (`TrunkLeanDeg`) come proxy**, non un vero calcolo del baricentro:
  `PoseFrame` non porta ancora un segnale dedicato all'equilibrio (richiederebbe la posizione dei
  piedi/caviglie e un modello di base di supporto). Il valore ROM riportato per questo esercizio è
  quindi un'approssimazione, non una vera metrica posturografica.
- **BreathFlower non ha alcun analizzatore**: l'espansione toracica richiede un segnale che
  `PoseFrame` non modella (distanza spalle/torace nel tempo, o un Face/Chest Landmarker dedicato).
  `ExerciseAnalysisPlan.RomJointFor` restituisce `null` per questo esercizio di proposito — la
  sessione viene comunque salvata, solo senza riepilogo clinico.
- **Clinician Portal (Blazor Server + SSR, live view dashboard) non implementato**: il brief lo
  descrive come app separata. `RehabHub.JoinTherapistGroup` esiste ed è stato verificato con un
  client SignalR reale (vedi sopra), quindi il "cablaggio" real-time funziona; manca la UI che lo
  consumerebbe. Farla bene (autenticazione clinico, elenco pazienti, replay p5.js del movimento)
  è un secondo progetto della stessa taglia di questo, non un'estensione di un'ora.
- **Nessuna autenticazione/autorizzazione**: `Home.razor` usa due GUID fissi come paziente/terapista
  demo. Un sistema reale ha bisogno quantomeno di ASP.NET Core Identity o un provider OIDC per
  distinguere pazienti e limitare `JoinTherapistGroup` al terapista giusto (oggi chiunque può unirsi
  al gruppo SignalR di un altro terapista, semplicemente conoscendone il GUID — vedi tabella rischi).
- **Offline-first / PWA (IndexedDB, service worker) non implementato**: il progetto Blazor è stato
  generato senza il flag `--pwa`. Il brief lo richiede esplicitamente ("Offline-first con
  IndexedDB"); aggiungerlo bene (coda di sessioni non sincronizzate, conflict resolution al rientro
  online) è un pezzo di lavoro a sé, non un checkbox.
- **.NET Aspire (dashboard SignalR latency) non usato**: utile quando ci sono più servizi/istanze
  da orchestrare in dev; con due soli servizi (Api, Web) più Postgres, `dotnet run` in due terminali
  o `docker compose` bastano.
- **Native AOT non abilitato**: stesso ragionamento di VitalFace — EF Core + Npgsql non hanno oggi
  un supporto AOT maturo e testato in produzione. Il trimming aggressivo del payload Blazor WASM
  (<2MB, altro obiettivo del brief) *non* è stato misurato in questa sessione.
- **Modelli MediaPipe (`.task`) non inclusi** nel repository (binari multi-MB, licenza Google): vanno
  scaricati e posizionati in `KineMotion.Web/wwwroot/models/` prima del deploy (vedi il README in
  quella cartella).

## Sicurezza e Compliance

- **Nessun video/frame/landmark grezzo lascia mai il browser**: `PoseFrameDto` porta solo 6 numeri
  in virgola mobile (angoli in gradi, posizione normalizzata del polso) — vedi il commento su
  `PoseFrame.cs`. È impossibile ricostruire un volto o un'identità da questi dati.
- **Dominio persistence-ignorant**: `Patient`, `Prescription`, `Session` (Core) non hanno alcuna
  dipendenza da EF Core; il mapping è tutto in `KineMotion.Infrastructure/Configurations`, quindi il
  dominio resta testabile in isolamento (i 13 test in `KineMotion.Core.Tests` non toccano mai un
  database) e sostituibile senza toccare la logica clinica.
- **CORS ristretto via configurazione** (`Cors:PatientOrigins`), nessuna origine wildcard di
  default — verificato in questa sessione: una richiesta da un'origine non in lista viene
  effettivamente bloccata dal browser (vedi log Playwright sopra), non solo "configurata a dovere
  sulla carta".
- **RehabHub non ha ancora controllo d'accesso ai gruppi** (vedi scostamenti sopra): è la lacuna di
  sicurezza più concreta di questo scaffold, non solo teorica.

## Rischi e Best Practice (produzione)

| Area | Rischio | Mitigazione consigliata |
|---|---|---|
| Autenticazione/autorizzazione | Nessuna in questo scaffold; chiunque può chiamare `JoinTherapistGroup` con un GUID indovinato o rubato | ASP.NET Core Identity o OIDC prima di qualsiasi pilota con dati pazienti reali; `RehabHub` deve verificare che il chiamante sia davvero quel terapista |
| Accuratezza clinica | ROM/smoothness/tremore sono stime da landmark 2D di una webcam, sensibili a inquadratura/illuminazione | Le soglie (`RomResult.CompensationThresholdDeg`, `TremorResult.TremorIndexThreshold`) sono bande "segnala per revisione umana", non soglie diagnostiche — vanno validate con un fisioterapista prima di finire in un referto |
| Modelli MediaPipe | File `.task` grandi, licenza Google, aggiornamenti nel tempo | Pinnare la versione, verificare hash all'avvio, pipeline di aggiornamento controllata (non "latest" da CDN in produzione) |
| Connessione DB | `EnableRetryOnFailure` già configurato, ma nessun circuit breaker applicativo | Aggiungere Polly se il volume di pazienti/sessioni concorrenti cresce oltre un singolo centro |
| Migrazioni EF Core | Auto-migrate abilitato solo in `Development` | Mantenere così; introdurre un job di migrazione esplicito nel pipeline CI/CD |
| Native AOT | Non abilitato (vedi sopra) | Non forzarlo finché Npgsql/EF Core non hanno supporto maturo e testato insieme |
| Live streaming SignalR | `StreamMetrics` è fire-and-forget e ignora i fallimenti (vedi `RehabHubClient`) | Corretto per la biofeedback in tempo reale; non usare questo canale per nulla che debba essere garantito — quello è il ruolo di `POST /api/sessions/complete` |
| Offline/PWA | Non implementato; una sessione con connessione persa a metà perde i dati non ancora inviati | Prima di un pilota in centri con wifi non affidabile, implementare la coda IndexedDB richiesta dal brief |

## Come proseguire

1. Scaricare `pose_landmarker_lite.task` e posizionarlo in `wwwroot/models/`; validare che
   `cdn.jsdelivr.net` sia raggiungibile dall'ambiente di deploy reale (non lo è in questo sandbox).
2. Test di accettazione end-to-end su hardware reale (webcam vera, browser con rete vera) per
   ShoulderBirdFlight, prima di investire negli altri quattro giochi.
3. Aggiungere autenticazione prima di qualsiasi pilota con dati pazienti reali (vedi tabella rischi).
4. Implementare gli analizzatori mancanti (baricentro per BalanceBoard, segnale toracico per
   BreathFlower) con un fisioterapista che validi il mapping clinico, non da soli.
5. Progettare il Clinician Portal come secondo modulo, riusando `RehabHub` così com'è.
