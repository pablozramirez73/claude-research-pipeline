#!/usr/bin/env bash
#
# Clones (or updates) the VitalFace Station branch, brings the app up with Docker Compose, and
# exposes both the API and the kiosk UI publicly via two Cloudflare Quick Tunnels — no Cloudflare
# account needed. Run this on a machine with real internet access (not inside a sandboxed CI/agent
# container): Cloudflare Quick Tunnels need outbound access to Cloudflare's edge network, which a
# locked-down egress proxy (e.g. a CI sandbox) will typically block.
#
# The API and the kiosk end up as two separate public hostnames (Cloudflare Quick Tunnels can't
# multiplex two local ports behind one hostname), so this script:
#   1. brings up postgres + api first,
#   2. opens a tunnel to the api and learns its public URL,
#   3. (re)starts the web container with that URL baked into its runtime config,
#   4. opens a tunnel to the web container and learns its public URL,
#   5. restarts the api container once more so its CORS policy allows that web URL.
#
# Usage:
#   ./run-with-cloudflare-tunnel.sh
#
# Optional environment overrides:
#   REPO_URL     (default: https://github.com/pablozramirez73/claude-research-pipeline.git)
#   BRANCH       (default: claude/vitalface-station-app-s60ij3 — switch to main once merged)
#   TARGET_DIR   (default: $HOME/VitalFaceStation/claude-research-pipeline — a fixed, absolute
#                 location, not one relative to wherever you happen to run this from; see below)
#   API_PORT     (default: 8080 — host port; change if already in use, e.g. by another app/VPN)
#   WEB_PORT     (default: 8081 — host port; same caveat)

set -euo pipefail

# Resolved before anything else touches the working directory, so it stays accurate regardless of
# any later 'cd' — needed below to detect whether this script is running from inside the very repo
# checkout it's about to 'git pull' (see the self-relaunch step after step 2).
SCRIPT_ABS_PATH="$(cd "$(dirname "$0")" >/dev/null 2>&1 && pwd)/$(basename "$0")"

REPO_URL="${REPO_URL:-https://github.com/pablozramirez73/claude-research-pipeline.git}"
BRANCH="${BRANCH:-claude/vitalface-station-app-s60ij3}"
# A fixed, absolute default (not a "./..." path) on purpose: re-running this script from a
# different working directory (or from inside a previous clone's own scripts/ folder) must always
# land on the exact same checkout. Otherwise you end up with multiple physical clones that all
# resolve to the same Docker Compose project name (derived from the "VitalFace" folder name) and
# therefore silently share the same Postgres volume across mismatched .env passwords — and, worse,
# a relative default can make a clone recurse into itself if run from inside an existing checkout.
TARGET_DIR="${TARGET_DIR:-$HOME/VitalFaceStation/claude-research-pipeline}"
API_PORT="${API_PORT:-8080}"
WEB_PORT="${WEB_PORT:-8081}"

CLOUDFLARED_BIN="${CLOUDFLARED_BIN:-cloudflared}"
API_TUNNEL_LOG="/tmp/vitalface-tunnel-api.log"
WEB_TUNNEL_LOG="/tmp/vitalface-tunnel-web.log"
API_TUNNEL_PID=""
WEB_TUNNEL_PID=""
API_TAIL_PID=""
WEB_TAIL_PID=""

log() { printf '\n\033[1;34m==> %s\033[0m\n' "$1"; }
die() { printf '\n\033[1;31mErrore: %s\033[0m\n' "$1" >&2; exit 1; }

case "$TARGET_DIR" in
    *claude-research-pipeline*claude-research-pipeline*)
        die "Il percorso di destinazione ('$TARGET_DIR') contiene più volte 'claude-research-pipeline' annidato — segno che una precedente esecuzione è stata lanciata da dentro un clone già esistente. Cancella quella cartella annidata, poi rilancia (userà '$HOME/VitalFaceStation/claude-research-pipeline' come percorso pulito e stabile)."
        ;;
esac

cleanup() {
    if [ -n "$API_TUNNEL_PID" ]; then kill "$API_TUNNEL_PID" 2>/dev/null || true; fi
    if [ -n "$WEB_TUNNEL_PID" ]; then kill "$WEB_TUNNEL_PID" 2>/dev/null || true; fi
    if [ -n "$API_TAIL_PID" ]; then kill "$API_TAIL_PID" 2>/dev/null || true; fi
    if [ -n "$WEB_TAIL_PID" ]; then kill "$WEB_TAIL_PID" 2>/dev/null || true; fi
}
trap cleanup EXIT INT TERM

# --- 1. Prerequisiti -------------------------------------------------------

command -v git >/dev/null 2>&1 || die "git non è installato."
command -v docker >/dev/null 2>&1 || die "docker non è installato — vedi https://docs.docker.com/engine/install/"
docker compose version >/dev/null 2>&1 || die "il plugin 'docker compose' non è disponibile (serve Docker 20.10+)."
docker info >/dev/null 2>&1 || die "il daemon Docker non risponde — avvialo (es. 'sudo systemctl start docker') e riprova."

# After bringing a service up, confirms Docker actually published its host port — not just that
# the container is running. A host port already held by something outside Docker/Compose (another
# app, a leftover process, sometimes a Windows/Hyper-V reserved range) can make `docker compose up`
# succeed while silently failing to publish, which otherwise looks identical to "the app is slow to
# start" — you get a real-looking (but wrong) HTTP response instead of a clear connection error.
verify_port_published() {
    service="$1"; container_port="$2"; host_port="$3"
    published="$(docker compose port "$service" "$container_port" 2>/dev/null || true)"
    if [ -z "$published" ]; then
        die "Docker non ha pubblicato la porta $container_port di '$service' sulla porta host $host_port. Causa probabile: un altro processo (non Docker) sta già usando la porta $host_port sul tuo computer. Verifica con 'netstat -ano | findstr :$host_port' (Windows) o 'lsof -i :$host_port' (macOS/Linux), poi rilancia con $(echo "$service" | tr '[:lower:]' '[:upper:]')_PORT=<altra porta> (es. 18080) ./run-with-cloudflare-tunnel.sh."
    fi
}

# --- 2. Clona o aggiorna il branch da GitHub -------------------------------

log "Recupero il codice da GitHub ($REPO_URL, branch $BRANCH)"
if [ -d "$TARGET_DIR/.git" ]; then
    git -C "$TARGET_DIR" fetch origin "$BRANCH"
    git -C "$TARGET_DIR" checkout "$BRANCH"
    git -C "$TARGET_DIR" pull --ff-only origin "$BRANCH"
else
    mkdir -p "$(dirname "$TARGET_DIR")"
    git clone --branch "$BRANCH" --single-branch "$REPO_URL" "$TARGET_DIR"
fi

# Self-relaunch from the just-updated file when this script is itself the file 'git pull' above may
# have just changed. Bash (like most shells) doesn't re-read a script from disk mid-execution — it
# keeps running whatever it already parsed — so without this, a fetched fix (like the one that
# added this exact log-file feature) would silently NOT take effect until a second, separate run.
# 'exec' replaces this process with the new one (no lingering parent), and the guard env var stops
# an infinite relaunch loop.
case "$SCRIPT_ABS_PATH" in
    "$TARGET_DIR"/*)
        if [ -z "${VITALFACE_TUNNEL_RELAUNCHED:-}" ]; then
            export VITALFACE_TUNNEL_RELAUNCHED=1 REPO_URL BRANCH TARGET_DIR API_PORT WEB_PORT
            log "Codice aggiornato da GitHub — rilancio lo script dalla versione appena scaricata"
            exec bash "$SCRIPT_ABS_PATH"
        fi
        ;;
esac

# Duplicate ALL of this script's output (stdout+stderr) to a log file, on top of the terminal, so a
# full run survives copy/paste issues, a closed terminal, or scrollback too short to hold a whole
# `docker compose build` — this has been the single biggest obstacle in diagnosing failures so far,
# since it means the exact output can just be attached/pasted from a file instead of re-typed.
LOG_FILE="${LOG_FILE:-$(dirname "$TARGET_DIR")/vitalface-run.log}"
mkdir -p "$(dirname "$LOG_FILE")"
exec > >(tee -a "$LOG_FILE") 2>&1
log "Log completo di questa esecuzione: $LOG_FILE"

cd "$TARGET_DIR/VitalFace"
URLS_FILE="$(pwd)/vitalface-tunnel-urls.txt"

# --- 3. Password del database -----------------------------------------------

ENV_FILE=".env"
touch "$ENV_FILE"

set_env_var() {
    key="$1"; value="$2"
    if grep -q "^${key}=" "$ENV_FILE" 2>/dev/null; then
        # portable in-place edit (works with both GNU and BSD sed)
        sed -i.bak "s|^${key}=.*|${key}=${value}|" "$ENV_FILE" && rm -f "$ENV_FILE.bak"
    else
        printf '%s=%s\n' "$key" "$value" >> "$ENV_FILE"
    fi
}

if ! grep -q '^VITALFACE_DB_PASSWORD=' "$ENV_FILE" 2>/dev/null; then
    log "Genero una password per PostgreSQL in .env"
    set_env_var VITALFACE_DB_PASSWORD "$(openssl rand -hex 16 2>/dev/null || head -c32 /dev/urandom | base64 | tr -dc 'a-zA-Z0-9' | head -c32)"
fi

set_env_var VITALFACE_API_PORT "$API_PORT"
set_env_var VITALFACE_WEB_PORT "$WEB_PORT"

# --- 4. Installa cloudflared se manca ---------------------------------------

if ! command -v "$CLOUDFLARED_BIN" >/dev/null 2>&1; then
    log "cloudflared non trovato, provo a installarlo"
    OS="$(uname -s)"
    ARCH="$(uname -m)"
    case "$OS" in
        Darwin)
            command -v brew >/dev/null 2>&1 || die "Homebrew non trovato — installa cloudflared manualmente: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/"
            brew install cloudflared
            ;;
        Linux)
            case "$ARCH" in
                x86_64) CF_ARCH="amd64" ;;
                aarch64|arm64) CF_ARCH="arm64" ;;
                *) die "Architettura '$ARCH' non supportata da questo script — installa cloudflared manualmente." ;;
            esac
            URL="https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-${CF_ARCH}"
            TMP_BIN="$(mktemp)"
            curl -fsSL "$URL" -o "$TMP_BIN" || die "download di cloudflared fallito da $URL"
            chmod +x "$TMP_BIN"
            if install -m 0755 "$TMP_BIN" /usr/local/bin/cloudflared 2>/dev/null; then
                :
            else
                sudo install -m 0755 "$TMP_BIN" /usr/local/bin/cloudflared || die "impossibile installare cloudflared in /usr/local/bin"
            fi
            rm -f "$TMP_BIN"
            CLOUDFLARED_BIN="/usr/local/bin/cloudflared"
            ;;
        *)
            die "Sistema operativo '$OS' non supportato da questo script — installa cloudflared manualmente."
            ;;
    esac
fi

log "cloudflared: $("$CLOUDFLARED_BIN" --version)"

# --- 5. Costruisci le immagini e avvia postgres + api -----------------------

log "Costruisco le immagini Docker (può richiedere qualche minuto la prima volta)"
docker compose build

log "Avvio PostgreSQL e l'API"
docker compose up -d postgres api
verify_port_published "api" "8080" "$API_PORT"

# Known, safe-to-auto-fix failure mode: re-running this script from a different clone of the same
# repo (same containing folder name -> same Docker Compose project name -> same shared Postgres
# volume) leaves an already-initialized database whose password no longer matches a freshly
# generated .env. There is no real data at stake in this demo/tunnel deployment, so recover by
# wiping the volume and letting Postgres re-initialize with the current .env password, instead of
# making the user diagnose and fix it by hand.
reset_postgres_volume() {
    printf '\n    Rilevata mancata corrispondenza di password con un volume Postgres preesistente — reinizializzo il database (nessun dato reale da perdere in questo demo).\n' >&2
    docker compose down -v
    docker compose up -d postgres api
    verify_port_published "api" "8080" "$API_PORT"
}

wait_for_http() {
    # 90 attempts * 2s = up to 3 minutes: the first time a container publishes a brand-new host
    # port, a host firewall (Windows Defender Firewall under WSL is the classic case) can silently
    # prompt to allow the container runtime's networking component through — while that prompt sits
    # unanswered, every connection attempt times out with no response at all, indistinguishable
    # from the container just being slow to start. Give it more room before giving up.
    url="$1"; label="$2"; service="$3"; total_attempts=90; attempts="$total_attempts"
    body_file="$(mktemp)"
    recovered=0
    saw_any_http_response=0
    while [ "$attempts" -gt 0 ]; do
        # --noproxy '*': a system-configured proxy would otherwise try to route this localhost
        # request through itself and fail, even though the container is perfectly reachable directly.
        http_code="$(curl -s --noproxy '*' -o "$body_file" -w '%{http_code}' "$url" 2>/dev/null || echo "000")"
        if [ "$http_code" = "200" ]; then
            rm -f "$body_file"
            return 0
        fi
        if [ "$http_code" != "000" ]; then
            saw_any_http_response=1
            # The server responded, just not with success — retrying blindly won't fix a bad
            # response, so either auto-recover (once) from a known cause, or fail fast with the
            # actual body instead of waiting out the full timeout.
            if [ "$service" = "api" ] && [ "$recovered" -eq 0 ] && grep -q "password authentication failed" "$body_file" 2>/dev/null; then
                reset_postgres_volume
                recovered=1
                attempts="$total_attempts"
                sleep 3
                continue
            fi
            printf '\n--- %s ha risposto con HTTP %s ---\n' "$label" "$http_code" >&2
            cat "$body_file" >&2 2>/dev/null
            printf '\n' >&2
            break
        fi
        if [ $(( (total_attempts - attempts) % 5 )) -eq 0 ] && [ "$attempts" -ne "$total_attempts" ]; then
            printf '    ... ancora in attesa: %s (%s)\n' "$label" "$url" >&2
        fi
        attempts=$((attempts - 1))
        sleep 2
    done
    rm -f "$body_file"
    printf '\n--- ultime righe di "docker compose logs %s" ---\n' "$service" >&2
    docker compose logs --tail 30 "$service" >&2 2>/dev/null || true
    firewall_hint=""
    if [ "$saw_any_http_response" -eq 0 ]; then
        # Every attempt failed at the connection level (never even got back a bad HTTP response) —
        # the classic signature of an unanswered host firewall prompt on a freshly published port
        # (Windows Defender Firewall under WSL is the common case), not a slow/broken container.
        firewall_hint=" Se il container risulta sano nei log sopra, controlla se e' comparsa (magari dietro un'altra finestra) una richiesta del firewall per consentire l'accesso alla rete — finche' resta senza risposta, ogni connessione viene bloccata in silenzio esattamente cosi'."
    fi
    die "$label non ha risposto positivamente su $url — vedi sopra (o 'docker compose logs $service').$firewall_hint"
}

wait_for_http "http://localhost:$API_PORT/health" "L'API" "api"
log "API pronta su http://localhost:$API_PORT"

# --- 6. Tunnel per l'API -----------------------------------------------------

extract_tunnel_url() {
    logfile="$1"; total_attempts=60; attempts="$total_attempts"
    while [ "$attempts" -gt 0 ]; do
        url="$(grep -oE 'https://[a-zA-Z0-9.-]+\.trycloudflare\.com' "$logfile" 2>/dev/null | head -n1 || true)"
        if [ -n "$url" ]; then
            printf '%s' "$url"
            return 0
        fi
        if [ $(( (total_attempts - attempts) % 10 )) -eq 0 ] && [ "$attempts" -ne "$total_attempts" ]; then
            printf '    ... ancora in attesa dell'"'"'URL del tunnel (guarda l'"'"'output di cloudflared qui sopra)\n' >&2
        fi
        attempts=$((attempts - 1))
        sleep 1
    done
    printf '\n--- ultime righe di %s ---\n' "$logfile" >&2
    tail -n 20 "$logfile" >&2 2>/dev/null || true
    return 1
}

# Writes (overwrites) a small, easy-to-find/share summary of the current tunnel URLs — called once
# per URL as soon as it's known, so the file is useful even if the script fails or is interrupted
# before both tunnels are up, not just at the very end.
write_urls_file() {
    {
        printf 'VitalFace Station — URL pubblici (Cloudflare Quick Tunnel)\n'
        printf 'Generato: %s\n\n' "$(date '+%Y-%m-%d %H:%M:%S %Z')"
        printf 'Kiosk (apri questo sul dispositivo/tablet): %s\n' "${WEB_URL:-<non ancora disponibile>}"
        printf 'API (uso interno, CORS ristretto al kiosk sopra): %s\n\n' "${API_URL:-<non ancora disponibile>}"
        printf 'Questi URL sono temporanei: cambiano ad ogni riavvio dello script e restano attivi\n'
        printf 'finché lo script resta in esecuzione.\n\n'
        printf 'Log completo di questa esecuzione: %s\n' "$LOG_FILE"
    } > "$URLS_FILE"
}

log "Apro il tunnel Cloudflare per l'API (porta $API_PORT) — output live qui sotto:"
"$CLOUDFLARED_BIN" tunnel --url "http://localhost:$API_PORT" > "$API_TUNNEL_LOG" 2>&1 &
API_TUNNEL_PID=$!
tail -n +1 -f "$API_TUNNEL_LOG" 2>/dev/null &
API_TAIL_PID=$!

API_URL="$(extract_tunnel_url "$API_TUNNEL_LOG")" || die "non sono riuscito a leggere l'URL del tunnel API entro 60s — controlla $API_TUNNEL_LOG"
log "API pubblica: $API_URL"
write_urls_file
log "URL scritto anche su file: $URLS_FILE"

# --- 7. Ricrea il kiosk con l'URL dell'API iniettato a runtime --------------

set_env_var VITALFACE_API_BASE_URL "${API_URL}/"

log "Avvio il kiosk Blazor con l'URL dell'API pubblico"
docker compose up -d --build web
verify_port_published "web" "8080" "$WEB_PORT"
wait_for_http "http://localhost:$WEB_PORT/" "Il kiosk" "web"
log "Kiosk pronto su http://localhost:$WEB_PORT"

# --- 8. Tunnel per il kiosk --------------------------------------------------

log "Apro il tunnel Cloudflare per il kiosk (porta $WEB_PORT) — output live qui sotto:"
"$CLOUDFLARED_BIN" tunnel --url "http://localhost:$WEB_PORT" > "$WEB_TUNNEL_LOG" 2>&1 &
WEB_TUNNEL_PID=$!
tail -n +1 -f "$WEB_TUNNEL_LOG" 2>/dev/null &
WEB_TAIL_PID=$!

WEB_URL="$(extract_tunnel_url "$WEB_TUNNEL_LOG")" || die "non sono riuscito a leggere l'URL del tunnel kiosk entro 60s — controlla $WEB_TUNNEL_LOG"
log "Kiosk pubblico: $WEB_URL"
write_urls_file
log "URL scritti anche su file: $URLS_FILE"

# --- 9. Aggiorna il CORS dell'API con l'origine pubblica del kiosk ----------

set_env_var VITALFACE_WEB_ORIGIN "$WEB_URL"

log "Riavvio l'API con il CORS aggiornato"
docker compose up -d --force-recreate api
wait_for_http "http://localhost:$API_PORT/health" "L'API (dopo il riavvio)" "api"

# --- Riepilogo ---------------------------------------------------------------

cat <<EOF

================================================================================
 VitalFace Station è online.

   Kiosk (apri questo sul dispositivo/tablet): ${WEB_URL}
   API (uso interno, CORS ristretto al kiosk sopra): ${API_URL}

 Entrambi gli URL sono HTTPS (necessario per l'accesso alla webcam via getUserMedia).
 Sono URL Cloudflare Quick Tunnel temporanei: cambiano ad ogni riavvio di questo script
 e restano attivi finché questo script resta in esecuzione.

 - I container Docker restano in esecuzione in background anche se fermi questo script.
   Per fermarli: (cd "$TARGET_DIR/VitalFace" && docker compose down)
 - Premi Ctrl+C qui per chiudere SOLO i due tunnel pubblici (l'app resta raggiungibile
   in locale su http://localhost:$WEB_PORT e http://localhost:$API_PORT).

 Gli URL qui sopra sono salvati anche su: $URLS_FILE
 Il log completo di questa esecuzione è in: $LOG_FILE
================================================================================

EOF

wait "$API_TUNNEL_PID" "$WEB_TUNNEL_PID"
