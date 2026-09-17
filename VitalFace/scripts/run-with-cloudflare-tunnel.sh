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
#   TARGET_DIR   (default: ./claude-research-pipeline)

set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/pablozramirez73/claude-research-pipeline.git}"
BRANCH="${BRANCH:-claude/vitalface-station-app-s60ij3}"
TARGET_DIR="${TARGET_DIR:-./claude-research-pipeline}"

CLOUDFLARED_BIN="${CLOUDFLARED_BIN:-cloudflared}"
API_TUNNEL_LOG="/tmp/vitalface-tunnel-api.log"
WEB_TUNNEL_LOG="/tmp/vitalface-tunnel-web.log"
API_TUNNEL_PID=""
WEB_TUNNEL_PID=""
API_TAIL_PID=""
WEB_TAIL_PID=""

log() { printf '\n\033[1;34m==> %s\033[0m\n' "$1"; }
die() { printf '\n\033[1;31mErrore: %s\033[0m\n' "$1" >&2; exit 1; }

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

# --- 2. Clona o aggiorna il branch da GitHub -------------------------------

log "Recupero il codice da GitHub ($REPO_URL, branch $BRANCH)"
if [ -d "$TARGET_DIR/.git" ]; then
    git -C "$TARGET_DIR" fetch origin "$BRANCH"
    git -C "$TARGET_DIR" checkout "$BRANCH"
    git -C "$TARGET_DIR" pull --ff-only origin "$BRANCH"
else
    git clone --branch "$BRANCH" --single-branch "$REPO_URL" "$TARGET_DIR"
fi

cd "$TARGET_DIR/VitalFace"

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

wait_for_http() {
    url="$1"; label="$2"; service="$3"; total_attempts=60; attempts="$total_attempts"
    while [ "$attempts" -gt 0 ]; do
        if curl -fsS -o /dev/null "$url" 2>/dev/null; then
            return 0
        fi
        if [ $(( (total_attempts - attempts) % 5 )) -eq 0 ] && [ "$attempts" -ne "$total_attempts" ]; then
            printf '    ... ancora in attesa: %s (%s)\n' "$label" "$url" >&2
        fi
        attempts=$((attempts - 1))
        sleep 2
    done
    printf '\n--- ultime righe di "docker compose logs %s" ---\n' "$service" >&2
    docker compose logs --tail 30 "$service" >&2 2>/dev/null || true
    die "$label non ha risposto in tempo su $url dopo 120s — vedi i log sopra (o 'docker compose logs $service')."
}

wait_for_http "http://localhost:8080/health" "L'API" "api"
log "API pronta su http://localhost:8080"

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

log "Apro il tunnel Cloudflare per l'API (porta 8080) — output live qui sotto:"
"$CLOUDFLARED_BIN" tunnel --url http://localhost:8080 > "$API_TUNNEL_LOG" 2>&1 &
API_TUNNEL_PID=$!
tail -n +1 -f "$API_TUNNEL_LOG" 2>/dev/null &
API_TAIL_PID=$!

API_URL="$(extract_tunnel_url "$API_TUNNEL_LOG")" || die "non sono riuscito a leggere l'URL del tunnel API entro 60s — controlla $API_TUNNEL_LOG"
log "API pubblica: $API_URL"

# --- 7. Ricrea il kiosk con l'URL dell'API iniettato a runtime --------------

set_env_var VITALFACE_API_BASE_URL "${API_URL}/"

log "Avvio il kiosk Blazor con l'URL dell'API pubblico"
docker compose up -d --build web
wait_for_http "http://localhost:8081/" "Il kiosk" "web"
log "Kiosk pronto su http://localhost:8081"

# --- 8. Tunnel per il kiosk --------------------------------------------------

log "Apro il tunnel Cloudflare per il kiosk (porta 8081) — output live qui sotto:"
"$CLOUDFLARED_BIN" tunnel --url http://localhost:8081 > "$WEB_TUNNEL_LOG" 2>&1 &
WEB_TUNNEL_PID=$!
tail -n +1 -f "$WEB_TUNNEL_LOG" 2>/dev/null &
WEB_TAIL_PID=$!

WEB_URL="$(extract_tunnel_url "$WEB_TUNNEL_LOG")" || die "non sono riuscito a leggere l'URL del tunnel kiosk entro 60s — controlla $WEB_TUNNEL_LOG"
log "Kiosk pubblico: $WEB_URL"

# --- 9. Aggiorna il CORS dell'API con l'origine pubblica del kiosk ----------

set_env_var VITALFACE_WEB_ORIGIN "$WEB_URL"

log "Riavvio l'API con il CORS aggiornato"
docker compose up -d --force-recreate api
wait_for_http "http://localhost:8080/health" "L'API (dopo il riavvio)" "api"

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
   in locale su http://localhost:8081 e http://localhost:8080).
================================================================================

EOF

wait "$API_TUNNEL_PID" "$WEB_TUNNEL_PID"
