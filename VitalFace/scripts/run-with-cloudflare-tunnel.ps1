#Requires -Version 7.0
<#
.SYNOPSIS
    Clones (or updates) the VitalFace Station branch, brings the app up with Docker Compose, and
    exposes both the API and the kiosk UI publicly via two Cloudflare Quick Tunnels.

.DESCRIPTION
    PowerShell port of scripts/run-with-cloudflare-tunnel.sh — see that file for the full design
    rationale. No Cloudflare account is needed (Quick Tunnels). Run this on a machine with real
    internet access, not inside a sandboxed CI/agent container: Quick Tunnels need outbound access
    to Cloudflare's edge network, which a locked-down egress proxy will typically block.

    A Cloudflare Quick Tunnel maps one hostname to one local port, so the API and the kiosk end up
    as two separate public hostnames. This script wires that dependency together:
      1. brings up postgres + api first,
      2. opens a tunnel to the api and learns its public URL,
      3. (re)starts the web container with that URL baked into its runtime config,
      4. opens a tunnel to the web container and learns its public URL,
      5. restarts the api container once more so its CORS policy allows that web URL.

.PARAMETER RepoUrl
    Git URL to clone. Defaults to the VitalFace Station repository.

.PARAMETER Branch
    Branch to check out. Defaults to the feature branch — switch to 'main' once merged.

.PARAMETER TargetDir
    Local directory to clone into (or reuse if it already exists). Defaults to a fixed location
    under your user profile (~/VitalFaceStation/claude-research-pipeline) rather than a path
    relative to wherever you happen to run this from — re-running the script from a different
    working directory (or from inside a previous clone's own scripts/ folder) must always land on
    the exact same checkout, or you end up with multiple physical clones that all resolve to the
    same Docker Compose project name (derived from the "VitalFace" folder name) and therefore
    silently share the same Postgres volume across mismatched .env passwords — and, worse, a
    relative default can make a clone recurse into itself if run from inside an existing checkout.

.PARAMETER ApiPort
    Host port for the API. Defaults to 8080. Change this if 8080 is already used by something else
    on your machine (another app, a VPN client, sometimes a Windows/Hyper-V reserved port range) —
    that shows up as the API container running fine but never actually reachable on localhost.

.PARAMETER WebPort
    Host port for the kiosk. Defaults to 8081. Same caveat as ApiPort.

.EXAMPLE
    .\run-with-cloudflare-tunnel.ps1

.EXAMPLE
    .\run-with-cloudflare-tunnel.ps1 -Branch main -TargetDir C:\src\vitalface

.EXAMPLE
    .\run-with-cloudflare-tunnel.ps1 -ApiPort 18080 -WebPort 18081
#>
[CmdletBinding()]
param(
    [string]$RepoUrl = 'https://github.com/pablozramirez73/claude-research-pipeline.git',
    [string]$Branch = 'claude/vitalface-station-app-s60ij3',
    [string]$TargetDir = (Join-Path $HOME 'VitalFaceStation/claude-research-pipeline'),
    [int]$ApiPort = 8080,
    [int]$WebPort = 8081
)

$ErrorActionPreference = 'Stop'
# PowerShell 7.3+ can turn ANY non-zero exit code from a native command (docker, git, cloudflared)
# into an immediate terminating exception when $ErrorActionPreference is 'Stop' — silently
# short-circuiting this script's own "if ($LASTEXITCODE -ne 0) { Fail ... }" checks before they
# ever run, with no visible message. This script is written entirely around checking
# $LASTEXITCODE itself, so restore that classic behavior explicitly.
$PSNativeCommandUseErrorActionPreference = $false

# Resolve to an absolute path up front regardless of the current working directory, and regardless
# of whether -TargetDir was left at its default or passed explicitly (possibly as a relative path)
# — this is what actually prevents the clone-into-itself/shared-volume mess described above.
$TargetDir = [System.IO.Path]::GetFullPath($TargetDir)
if ($TargetDir -like "*claude-research-pipeline*claude-research-pipeline*") {
    Fail "Il percorso di destinazione risolto ('$TargetDir') contiene piu' volte 'claude-research-pipeline' annidato — segno che una precedente esecuzione e' stata lanciata da dentro un clone gia' esistente. Cancella quella cartella annidata ed esegui questo script di nuovo (userà '$([System.IO.Path]::GetFullPath((Join-Path $HOME 'VitalFaceStation/claude-research-pipeline')))' come percorso pulito e stabile)."
}

$script:CloudflaredPath = $null
$script:ApiTunnelProcess = $null
$script:WebTunnelProcess = $null
$ApiTunnelLog = Join-Path ([System.IO.Path]::GetTempPath()) 'vitalface-tunnel-api.log'
$WebTunnelLog = Join-Path ([System.IO.Path]::GetTempPath()) 'vitalface-tunnel-web.log'

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Fail {
    param([string]$Message)
    Write-Host ""
    Write-Host "Errore: $Message" -ForegroundColor Red
    exit 1
}

function Test-CommandExists {
    param([string]$Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

function Set-EnvFileVar {
    param([string]$Path, [string]$Key, [string]$Value)
    $lines = if (Test-Path $Path) { Get-Content $Path } else { @() }
    $pattern = "^$([regex]::Escape($Key))="
    $newLine = "$Key=$Value"
    $found = $false
    $updated = foreach ($line in $lines) {
        if ($line -match $pattern) { $found = $true; $newLine } else { $line }
    }
    if (-not $found) { $updated = @($updated) + $newLine }
    Set-Content -Path $Path -Value $updated
}

function Reset-PostgresVolume {
    # Known, safe-to-auto-fix failure mode: re-running this script from a different clone of the
    # same repo (same containing folder name -> same Docker Compose project name -> same shared
    # Postgres volume) leaves an already-initialized database whose password no longer matches a
    # freshly generated .env. There is no real data at stake in this demo/tunnel deployment, so
    # recover by wiping the volume and letting Postgres re-initialize with the current .env
    # password, instead of making the user diagnose and fix it by hand.
    Write-Host ""
    Write-Host "    Rilevata mancata corrispondenza di password con un volume Postgres preesistente — reinizializzo il database (nessun dato reale da perdere in questo demo)."
    docker compose down -v
    docker compose up -d postgres api
    Test-PortPublished -Service 'api' -ContainerPort 8080 -HostPort $ApiPort
}

function Wait-ForHttp {
    param([string]$Url, [string]$Label, [string]$Service, [int]$Attempts = 60)
    $totalAttempts = $Attempts
    $remaining = $Attempts
    $recovered = $false
    while ($remaining -gt 0) {
        try {
            # -NoProxy: a system/VPN proxy (common on corporate Windows machines) would otherwise
            # try to route this localhost request through itself and fail, even though the
            # container is perfectly healthy and reachable directly.
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 3 -NoProxy
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) { return }
        }
        catch [Microsoft.PowerShell.Commands.HttpResponseException] {
            # The server responded, just not with success — retrying blindly won't fix a bad
            # response, so either auto-recover (once) from a known cause, or fail fast with the
            # actual body instead of waiting out the full timeout.
            $body = $null
            try { $body = $_.ErrorDetails.Message } catch { }
            if ($Service -eq 'api' -and -not $recovered -and $body -and $body -match 'password authentication failed') {
                Reset-PostgresVolume
                $recovered = $true
                $remaining = $totalAttempts
                Start-Sleep -Seconds 3
                continue
            }
            Write-Host ""
            Write-Host "--- $Label ha risposto con errore ---"
            if ($body) { Write-Host $body }
            break
        }
        catch { }
        if (($totalAttempts - $remaining) -gt 0 -and ($totalAttempts - $remaining) % 5 -eq 0) {
            Write-Host "    ... ancora in attesa: $Label ($Url)"
        }
        $remaining--
        Start-Sleep -Seconds 2
    }
    Write-Host ""
    Write-Host "--- ultime righe di 'docker compose logs $Service' ---"
    docker compose logs --tail 30 $Service
    Fail "$Label non ha risposto positivamente su $Url — vedi sopra (o 'docker compose logs $Service')."
}

function Get-TunnelUrl {
    # Streams cloudflared's own output to the console live (both the primary log and its stderr
    # sibling), exactly as if it were run directly — so the URL is visible even if the regex
    # match below is ever thrown off by a future cloudflared output format change.
    param([string]$LogPath, [int]$Attempts = 60)
    $linesShown = 0
    for ($i = 0; $i -lt $Attempts; $i++) {
        foreach ($candidate in @($LogPath, "$LogPath.err")) {
            if (Test-Path $candidate) {
                $content = Get-Content $candidate -ErrorAction SilentlyContinue
                if ($content.Count -gt $linesShown) {
                    $content[$linesShown..($content.Count - 1)] | ForEach-Object { Write-Host $_ }
                    $linesShown = $content.Count
                }
                $match = $content | Select-String -Pattern 'https://[a-zA-Z0-9.-]+\.trycloudflare\.com' | Select-Object -First 1
                if ($match) { return $match.Matches[0].Value }
            }
        }
        if ($i -gt 0 -and $i % 10 -eq 0) {
            Write-Host "    ... ancora in attesa dell'URL del tunnel (guarda l'output di cloudflared qui sopra)"
        }
        Start-Sleep -Seconds 1
    }
    Write-Host ""
    Write-Host "--- ultime righe di $LogPath ---"
    Get-Content $LogPath -Tail 20 -ErrorAction SilentlyContinue
    return $null
}

function Start-CloudflaredTunnel {
    param([string]$LocalUrl, [string]$LogPath)
    if (Test-Path $LogPath) { Remove-Item $LogPath -Force }
    return Start-Process -FilePath $script:CloudflaredPath `
        -ArgumentList @('tunnel', '--url', $LocalUrl) `
        -RedirectStandardOutput $LogPath `
        -RedirectStandardError "$LogPath.err" `
        -NoNewWindow -PassThru
}

function Test-PortPublished {
    # After bringing a service up, confirms Docker actually published its host port — not just
    # that the container is running. A host port already held by something outside Docker/Compose
    # (another app, a leftover process, sometimes a Windows/Hyper-V reserved range) can make
    # `docker compose up` succeed while silently failing to publish, which otherwise looks
    # identical to "the app is slow to start": you get a real-looking (but wrong) HTTP response
    # instead of a clear connection error.
    param([string]$Service, [int]$ContainerPort, [int]$HostPort)
    $published = docker compose port $Service $ContainerPort 2>$null
    if (-not $published) {
        Fail ("Docker non ha pubblicato la porta $ContainerPort di '$Service' sulla porta host $HostPort. " +
            "Causa probabile: un altro processo (non Docker) sta gia' usando la porta $HostPort sul tuo computer. " +
            "Verifica con 'netstat -ano | findstr :$HostPort', poi rilancia con " +
            "-$(($Service).Substring(0,1).ToUpper() + $Service.Substring(1))Port <altra porta> (es. 18080).")
    }
}

try {
# --- 1. Prerequisiti --------------------------------------------------------

if (-not (Test-CommandExists 'git')) { Fail 'git non è installato.' }
if (-not (Test-CommandExists 'docker')) { Fail 'docker non è installato — vedi https://docs.docker.com/desktop/setup/install/windows-install/' }
try { docker compose version | Out-Null } catch { Fail "il plugin 'docker compose' non è disponibile (serve Docker Desktop 20.10+)." }
try { docker info | Out-Null } catch { Fail 'il daemon Docker non risponde — avvia Docker Desktop e riprova.' }

# --- 2. Clona o aggiorna il branch da GitHub --------------------------------

Write-Step "Recupero il codice da GitHub ($RepoUrl, branch $Branch)"
if (Test-Path (Join-Path $TargetDir '.git')) {
    git -C $TargetDir fetch origin $Branch
    if ($LASTEXITCODE -ne 0) { Fail 'git fetch fallito.' }
    git -C $TargetDir checkout $Branch
    if ($LASTEXITCODE -ne 0) { Fail 'git checkout fallito.' }
    git -C $TargetDir pull --ff-only origin $Branch
    if ($LASTEXITCODE -ne 0) { Fail 'git pull fallito.' }
}
else {
    $parentDir = Split-Path $TargetDir -Parent
    if ($parentDir -and -not (Test-Path $parentDir)) {
        New-Item -ItemType Directory -Force -Path $parentDir | Out-Null
    }
    git clone --branch $Branch --single-branch $RepoUrl $TargetDir
    if ($LASTEXITCODE -ne 0) { Fail 'git clone fallito.' }
}

Set-Location (Join-Path $TargetDir 'VitalFace')

# --- 3. Password del database -----------------------------------------------

$EnvFile = '.env'
if (-not (Test-Path $EnvFile)) { New-Item -ItemType File -Path $EnvFile | Out-Null }

if (-not (Select-String -Path $EnvFile -Pattern '^VITALFACE_DB_PASSWORD=' -Quiet -ErrorAction SilentlyContinue)) {
    Write-Step 'Genero una password per PostgreSQL in .env'
    Set-EnvFileVar -Path $EnvFile -Key 'VITALFACE_DB_PASSWORD' -Value ([System.Guid]::NewGuid().ToString('N') + [System.Guid]::NewGuid().ToString('N'))
}

Set-EnvFileVar -Path $EnvFile -Key 'VITALFACE_API_PORT' -Value $ApiPort
Set-EnvFileVar -Path $EnvFile -Key 'VITALFACE_WEB_PORT' -Value $WebPort

# --- 4. Installa cloudflared se manca ---------------------------------------

if (Test-CommandExists 'cloudflared') {
    $script:CloudflaredPath = (Get-Command cloudflared).Source
}
else {
    Write-Step 'cloudflared non trovato, provo a installarlo'
    if (Test-CommandExists 'winget') {
        winget install --id Cloudflare.cloudflared -e --accept-source-agreements --accept-package-agreements
        if (Test-CommandExists 'cloudflared') {
            $script:CloudflaredPath = (Get-Command cloudflared).Source
        }
    }

    if (-not $script:CloudflaredPath) {
        $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
            'Arm64' { 'arm64' }
            default { 'amd64' }
        }
        $installDir = Join-Path $env:LOCALAPPDATA 'VitalFace\bin'
        New-Item -ItemType Directory -Force -Path $installDir | Out-Null
        $dest = Join-Path $installDir 'cloudflared.exe'
        $url = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-$arch.exe"
        Write-Host "Scarico $url"
        try {
            Invoke-WebRequest -Uri $url -OutFile $dest
        }
        catch {
            Fail "download di cloudflared fallito da $url — installalo manualmente: https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/"
        }
        $env:PATH = "$installDir;$env:PATH"
        $script:CloudflaredPath = $dest
    }
}

Write-Step "cloudflared: $(& $script:CloudflaredPath --version)"

# --- 5. Costruisci le immagini e avvia postgres + api -----------------------

Write-Step 'Costruisco le immagini Docker (può richiedere qualche minuto la prima volta)'
docker compose build
if ($LASTEXITCODE -ne 0) { Fail 'docker compose build fallito.' }

Write-Step 'Avvio PostgreSQL e l''API'
docker compose up -d postgres api
if ($LASTEXITCODE -ne 0) { Fail 'docker compose up (postgres, api) fallito.' }
Test-PortPublished -Service 'api' -ContainerPort 8080 -HostPort $ApiPort

Wait-ForHttp -Url "http://localhost:$ApiPort/health" -Label "L'API" -Service 'api'
Write-Step "API pronta su http://localhost:$ApiPort"

# --- 6. Tunnel per l'API -----------------------------------------------------

Write-Step "Apro il tunnel Cloudflare per l'API (porta $ApiPort)"
$script:ApiTunnelProcess = Start-CloudflaredTunnel -LocalUrl "http://localhost:$ApiPort" -LogPath $ApiTunnelLog

$ApiUrl = Get-TunnelUrl -LogPath $ApiTunnelLog
if (-not $ApiUrl) { Fail "non sono riuscito a leggere l'URL del tunnel API entro 60s — controlla $ApiTunnelLog" }
Write-Step "API pubblica: $ApiUrl"

# --- 7. Ricrea il kiosk con l'URL dell'API iniettato a runtime --------------

Set-EnvFileVar -Path $EnvFile -Key 'VITALFACE_API_BASE_URL' -Value "$ApiUrl/"

Write-Step "Avvio il kiosk Blazor con l'URL dell'API pubblico"
docker compose up -d --build web
if ($LASTEXITCODE -ne 0) { Fail 'docker compose up (web) fallito.' }
Test-PortPublished -Service 'web' -ContainerPort 8080 -HostPort $WebPort

Wait-ForHttp -Url "http://localhost:$WebPort/" -Label 'Il kiosk' -Service 'web'
Write-Step "Kiosk pronto su http://localhost:$WebPort"

# --- 8. Tunnel per il kiosk --------------------------------------------------

Write-Step "Apro il tunnel Cloudflare per il kiosk (porta $WebPort)"
$script:WebTunnelProcess = Start-CloudflaredTunnel -LocalUrl "http://localhost:$WebPort" -LogPath $WebTunnelLog

$WebUrl = Get-TunnelUrl -LogPath $WebTunnelLog
if (-not $WebUrl) { Fail "non sono riuscito a leggere l'URL del tunnel kiosk entro 60s — controlla $WebTunnelLog" }
Write-Step "Kiosk pubblico: $WebUrl"

# --- 9. Aggiorna il CORS dell'API con l'origine pubblica del kiosk ----------

Set-EnvFileVar -Path $EnvFile -Key 'VITALFACE_WEB_ORIGIN' -Value $WebUrl

Write-Step 'Riavvio l''API con il CORS aggiornato'
docker compose up -d --force-recreate api
if ($LASTEXITCODE -ne 0) { Fail 'docker compose up --force-recreate api fallito.' }

Wait-ForHttp -Url "http://localhost:$ApiPort/health" -Label "L'API (dopo il riavvio)" -Service 'api'

# --- Riepilogo ---------------------------------------------------------------

Write-Host ""
Write-Host '================================================================================' -ForegroundColor Green
Write-Host ' VitalFace Station è online.' -ForegroundColor Green
Write-Host ""
Write-Host "   Kiosk (apri questo sul dispositivo/tablet): $WebUrl"
Write-Host "   API (uso interno, CORS ristretto al kiosk sopra): $ApiUrl"
Write-Host ""
Write-Host ' Entrambi gli URL sono HTTPS (necessario per l''accesso alla webcam via getUserMedia).'
Write-Host ' Sono URL Cloudflare Quick Tunnel temporanei: cambiano ad ogni riavvio di questo'
Write-Host ' script e restano attivi finché questo script resta in esecuzione.'
Write-Host ""
Write-Host " - I container Docker restano in esecuzione in background anche se fermi questo script."
Write-Host "   Per fermarli: Set-Location '$((Get-Location).Path)'; docker compose down"
Write-Host ' - Premi Ctrl+C qui per chiudere SOLO i due tunnel pubblici (l''app resta raggiungibile'
Write-Host "   in locale su http://localhost:$WebPort e http://localhost:$ApiPort)."
Write-Host '================================================================================' -ForegroundColor Green
Write-Host ""

try {
    Wait-Process -Id $script:ApiTunnelProcess.Id, $script:WebTunnelProcess.Id
}
finally {
    if ($script:ApiTunnelProcess -and -not $script:ApiTunnelProcess.HasExited) {
        Stop-Process -Id $script:ApiTunnelProcess.Id -Force -ErrorAction SilentlyContinue
    }
    if ($script:WebTunnelProcess -and -not $script:WebTunnelProcess.HasExited) {
        Stop-Process -Id $script:WebTunnelProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
}
catch {
    # Last-resort safety net: anything that throws anywhere above and isn't already one of our
    # own Fail() calls (Fail exits directly, so it never reaches here) lands here instead of
    # silently dropping back to the prompt with no explanation — which is exactly what made the
    # earlier "it just stops with no error" reports so hard to diagnose.
    Write-Host ""
    Write-Host "Errore PowerShell non gestito: $($_.Exception.GetType().FullName): $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
