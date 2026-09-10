#!/usr/bin/env pwsh
# One-shot: bring up the dev stack (postgres + backend + frontend + dozzle),
# wait for the backend to answer, then seed a demo user with data.
#
#   ./scripts/start-all.ps1              # up + wait + seed
#   ./scripts/start-all.ps1 -NoSeed      # up + wait, skip seeding
#   ./scripts/start-all.ps1 -Fresh       # wipe the db volume first, then up + wait + seed
#   ./scripts/start-all.ps1 -Down        # tear the stack down and exit
#   ./scripts/start-all.ps1 -Rebuild     # force image rebuild on up

[CmdletBinding()]
param(
    [switch]$NoSeed,
    [switch]$Down,
    [switch]$Fresh,
    [switch]$Rebuild,
    [int]$TimeoutSeconds = 240
)

$ErrorActionPreference = "Stop"
$repo    = Split-Path $PSScriptRoot -Parent
$compose = @("compose", "-f", (Join-Path $repo "docker-compose.dev.yml"))

# --- docker reachable? ---
try { docker info *> $null } catch {
    throw "Docker no responde. Arranca Docker Desktop y reintenta."
}

if ($Down) {
    Write-Host "-> docker compose down ..."
    docker @compose down
    exit 0
}

Push-Location $repo
try {
    if ($Fresh) {
        Write-Host "-> Borrando datos previos (docker compose down -v) ..."
        docker @compose down -v
    }

    Write-Host "-> Levantando el stack (docker compose up -d) ..."
    $upArgs = @("up", "-d")
    if ($Rebuild) { $upArgs += "--build" }
    docker @compose @upArgs

    # --- wait for backend ---
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $ok = $false
    Write-Host -NoNewline "-> Esperando al backend en http://localhost:8080 "
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-WebRequest -Uri "http://localhost:8080/swagger/index.html" `
                -UseBasicParsing -TimeoutSec 3
            if ($r.StatusCode -eq 200) { $ok = $true; break }
        } catch { }
        Write-Host -NoNewline "."
        Start-Sleep -Seconds 3
    }
    Write-Host ""
    if (-not $ok) {
        Write-Warning "El backend no respondio en $TimeoutSeconds s (la 1a compilacion de 'dotnet watch' tarda)."
        Write-Warning "Mira los logs:  docker compose -f docker-compose.dev.yml logs -f backend    (o http://localhost:9999)"
        exit 1
    }
    Write-Host "   backend arriba."

    if (-not $NoSeed) {
        Write-Host ""
        & (Join-Path $PSScriptRoot "seed-demo.ps1")
    }

    Write-Host ""
    Write-Host "Todo listo:"
    Write-Host "  frontend -> http://localhost:8090"
    Write-Host "  backend  -> http://localhost:8080/swagger"
    Write-Host "  logs     -> http://localhost:9999"
}
finally {
    Pop-Location
}
