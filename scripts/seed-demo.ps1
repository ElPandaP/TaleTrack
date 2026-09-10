#!/usr/bin/env pwsh
# Seed a demo user with tracking data so you can log in and see a populated UI.
#
# Prereq: dev stack running ->  docker compose -f docker-compose.dev.yml up -d
# Then:                          ./scripts/seed-demo.ps1
# Log in at http://localhost:8090/login with the credentials printed at the end.
# Seeds ~65 books (so the carousel's 50-item cap + "see all" is visible), 3 movies, 3 series.
# Idempotent-ish: re-running adds more tracking events but media dedupes by title.
#
# Needs a FRESH database: users created before the switch to PBKDF2 password hashing had
# their old hashes invalidated by a migration, so login fails for them. Use
# `./scripts/start-all.ps1 -Fresh` (drops the db volume) if login errors out below.

[CmdletBinding()]
param(
    [string]$ApiBase  = "http://localhost:8080/api",
    [string]$Email    = "demo@taletrack.dev",
    [string]$Username  = "demo",
    [string]$Password  = "demo1234"
)

$ErrorActionPreference = "Stop"

# INTERNAL_API_KEY is needed to hit /register
$envFile = Join-Path $PSScriptRoot "..\.env"
$internalKey = ((Get-Content $envFile | Where-Object { $_ -match '^INTERNAL_API_KEY=' }) -replace '^INTERNAL_API_KEY=', '').Trim()
if (-not $internalKey) { throw "INTERNAL_API_KEY not found in $envFile" }

Write-Host "-> Registering $Email ..."
try {
    Invoke-RestMethod -Method Post -Uri "$ApiBase/register" `
        -Headers @{ "X-Internal-Api-Key" = $internalKey } `
        -ContentType "application/json" `
        -Body (@{ email = $Email; username = $Username; password = $Password } | ConvertTo-Json) | Out-Null
    Write-Host "   registered."
} catch {
    if ("$($_.ErrorDetails.Message)" -match "email_taken|already registered|registrado") {
        Write-Host "   already exists, continuing."
    } else { throw }
}

Write-Host "-> Logging in ..."
try {
    $login = Invoke-RestMethod -Method Post -Uri "$ApiBase/login" `
        -ContentType "application/json" `
        -Body (@{ email = $Email; password = $Password } | ConvertTo-Json)
} catch {
    Write-Host ""
    Write-Warning "No se pudo iniciar sesion como $Email."
    Write-Warning "Causa mas probable: la BD de dev tiene usuarios creados antes del cambio a hashing"
    Write-Warning "PBKDF2; la migracion 'ClearLegacyPasswordHashes' invalido sus contrasenas antiguas."
    Write-Warning "Solucion: borra el volumen de la BD y vuelve a arrancar con datos frescos:"
    Write-Warning "   ./scripts/start-all.ps1 -Fresh"
    throw
}
$auth = @{ Authorization = "Bearer $($login.token)" }

# The API splits tracking by media type (/tracking/books|movies|series) with different
# request bodies. This helper routes a seed item to the right endpoint.
function Add-Tracking {
    param($Headers, $Item)

    switch ($Item.type) {
        "Book" {
            $body = @{ title = $Item.title; progress = $Item.progress }
            if ($Item.author) { $body.author = $Item.author }
            if ($Item.isbn)   { $body.isbn   = $Item.isbn }
            if ($Item.length) { $body.pages  = [int]$Item.length }
            Invoke-RestMethod -Method Post -Uri "$ApiBase/tracking/books" -Headers $Headers `
                -ContentType "application/json" -Body ($body | ConvertTo-Json) | Out-Null
        }
        "Movie" {
            $body = @{ title = $Item.title; progress = $Item.progress }
            if ($Item.length) { $body.minutes = [int]$Item.length }
            Invoke-RestMethod -Method Post -Uri "$ApiBase/tracking/movies" -Headers $Headers `
                -ContentType "application/json" -Body ($body | ConvertTo-Json) | Out-Null
        }
        "Series" {
            # Old seed model was one row per series; the API is now per-episode.
            # Fake a season 1 with a few episodes so the library and stats look real.
            $totalEps = if ($Item.length -and [int]$Item.length -le 24) { [int]$Item.length } else { 8 }
            $watched  = if ([int]$Item.progress -ge 100) { $totalEps }
                        else { [Math]::Max(1, [int][Math]::Ceiling($totalEps * [int]$Item.progress / 100.0)) }
            for ($ep = 1; $ep -le $watched; $ep++) {
                $body = @{ title = $Item.title; season = 1; episode = $ep; minutes = 45; progress = 100 }
                Invoke-RestMethod -Method Post -Uri "$ApiBase/tracking/series" -Headers $Headers `
                    -ContentType "application/json" -Body ($body | ConvertTo-Json) | Out-Null
            }
        }
    }
}

# type must be one of: Book | Movie | Series   (see AddTrackingEventRequest)
# Books with a real ISBN get author + cover auto-filled from OpenLibrary in the background.
$items = @(
    @{ title = "The Hobbit";                        type = "Book";   length = 310; progress = 100; author = "J.R.R. Tolkien";   isbn = "9780547928227" }
    @{ title = "Dune";                              type = "Book";   length = 688; progress = 64;  author = "Frank Herbert";    isbn = "9780441013593" }
    @{ title = "Project Hail Mary";                 type = "Book";   length = 496; progress = 100; author = "Andy Weir";        isbn = "9780593135204" }
    @{ title = "The Name of the Wind";              type = "Book";   length = 662; progress = 27;  author = "Patrick Rothfuss"; isbn = "9780756404741" }
    @{ title = "Klara and the Sun";                 type = "Book";   length = 320; progress = 100; author = "Kazuo Ishiguro";   isbn = "9780593318171" }
    @{ title = "Piranesi";                          type = "Book";   length = 245; progress = 88;  author = "Susanna Clarke";   isbn = "9781635575637" }
    @{ title = "Blade Runner 2049";                 type = "Movie";  length = 164; progress = 100 }
    @{ title = "Everything Everywhere All at Once"; type = "Movie";  length = 139; progress = 100 }
    @{ title = "Dune: Part Two";                    type = "Movie";  length = 166; progress = 45  }
    @{ title = "Severance";                         type = "Series"; length = 9;   progress = 100 }
    @{ title = "The Bear";                          type = "Series"; length = 10;  progress = 70  }
    @{ title = "Shogun";                            type = "Series"; length = 10;  progress = 30  }
)

# A bigger book catalog so the carousel's 50-item cap is visible ("50 · see all").
# No ISBN -> these render the typed placeholder tile (fine for a demo).
$bookCatalog = @'
The Fellowship of the Ring;J.R.R. Tolkien;423
The Two Towers;J.R.R. Tolkien;352
The Return of the King;J.R.R. Tolkien;416
A Game of Thrones;George R.R. Martin;694
A Clash of Kings;George R.R. Martin;768
The Way of Kings;Brandon Sanderson;1007
Mistborn: The Final Empire;Brandon Sanderson;541
The Fifth Season;N.K. Jemisin;468
A Wizard of Earthsea;Ursula K. Le Guin;205
The Left Hand of Darkness;Ursula K. Le Guin;304
Neuromancer;William Gibson;271
Snow Crash;Neal Stephenson;468
The Three-Body Problem;Liu Cixin;400
Foundation;Isaac Asimov;244
I, Robot;Isaac Asimov;224
Do Androids Dream of Electric Sheep?;Philip K. Dick;210
Hyperion;Dan Simmons;482
Ender's Game;Orson Scott Card;324
The Dispossessed;Ursula K. Le Guin;341
Rendezvous with Rama;Arthur C. Clarke;256
2001: A Space Odyssey;Arthur C. Clarke;297
Ringworld;Larry Niven;342
The Forever War;Joe Haldeman;278
A Canticle for Leibowitz;Walter M. Miller Jr.;320
Solaris;Stanislaw Lem;204
Roadside Picnic;Arkady and Boris Strugatsky;209
The Stars My Destination;Alfred Bester;258
Childhood's End;Arthur C. Clarke;224
The Martian;Andy Weir;369
Recursion;Blake Crouch;336
Dark Matter;Blake Crouch;342
Station Eleven;Emily St. John Mandel;333
The Road;Cormac McCarthy;287
Never Let Me Go;Kazuo Ishiguro;288
The Remains of the Day;Kazuo Ishiguro;258
Cloud Atlas;David Mitchell;509
The Night Circus;Erin Morgenstern;387
Jonathan Strange & Mr Norrell;Susanna Clarke;782
The Priory of the Orange Tree;Samantha Shannon;830
Circe;Madeline Miller;393
The Song of Achilles;Madeline Miller;352
Gideon the Ninth;Tamsyn Muir;448
The Poppy War;R.F. Kuang;545
Babel;R.F. Kuang;546
The City & the City;China Mieville;312
Perdido Street Station;China Mieville;710
American Gods;Neil Gaiman;465
The Ocean at the End of the Lane;Neil Gaiman;181
Good Omens;Neil Gaiman and Terry Pratchett;288
Small Gods;Terry Pratchett;384
Guards! Guards!;Terry Pratchett;355
Mort;Terry Pratchett;272
The Colour of Magic;Terry Pratchett;288
A Deepness in the Sky;Vernor Vinge;774
Blindsight;Peter Watts;384
Children of Time;Adrian Tchaikovsky;600
The Long Way to a Small, Angry Planet;Becky Chambers;518
A Memory Called Empire;Arkady Martine;462
This Is How You Lose the Time War;Amal El-Mohtar and Max Gladstone;209
'@ -split "`n" | Where-Object { $_.Trim() }

$i = 0
foreach ($line in $bookCatalog) {
    $p = $line.Split(';')
    # mostly finished, a few in progress, a couple barely started
    $prog = switch ($i % 7) { 5 { 100 } 6 { (30, 55, 78 | Get-Random) } default { 100 } }
    $items += @{ title = $p[0].Trim(); type = "Book"; length = [int]$p[2].Trim(); progress = $prog; author = $p[1].Trim() }
    $i++
}

Write-Host "-> Adding $($items.Count) tracking events ..."
foreach ($it in $items) {
    Add-Tracking $auth $it
    Write-Host ("   + {0,-38} {1,-7} {2}%" -f $it.title, $it.type, $it.progress)
}

# ─────────────────────────────────────────────────────────────────────────────
#  Friends & test users
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "-> Seeding friends (alice, bob, carol) ..."

$avatar = { param($seed) "https://api.dicebear.com/9.x/notionists/svg?seed=$seed" }
$shareAll = @{
    bookProgress = $true; bookReviews = $true
    movieProgress = $true; movieReviews = $true
    seriesProgress = $true; seriesReviews = $true
}

function New-SeedUser {
    param([string]$Name, [string]$Mail, [string]$Seed, [array]$Titles)

    try {
        Invoke-RestMethod -Method Post -Uri "$ApiBase/register" `
            -Headers @{ "X-Internal-Api-Key" = $internalKey } -ContentType "application/json" `
            -Body (@{ email = $Mail; username = $Name; password = $Password } | ConvertTo-Json) | Out-Null
    } catch {
        if ("$($_.ErrorDetails.Message)" -notmatch "email_taken|already registered|registrado") { throw }
    }

    $lg = Invoke-RestMethod -Method Post -Uri "$ApiBase/login" -ContentType "application/json" `
        -Body (@{ email = $Mail; password = $Password } | ConvertTo-Json)
    $h  = @{ Authorization = "Bearer $($lg.token)" }
    $hk = @{ Authorization = "Bearer $($lg.token)"; "X-Internal-Api-Key" = $internalKey }
    $me = Invoke-RestMethod -Uri "$ApiBase/user/me" -Headers $h

    # avatar + share everything (per-media-type privacy: 3 types x {progress, reviews})
    Invoke-RestMethod -Method Put -Uri "$ApiBase/user/$($me.data.id)" -Headers $hk -ContentType "application/json" `
        -Body (@{ avatarUrl = (& $avatar $Seed); privacy = $shareAll } | ConvertTo-Json) | Out-Null

    foreach ($ti in $Titles) {
        $prog = if ($ti[2]) { [int]$ti[2] } else { 100 }
        $len  = if ($ti[1] -eq "Series") { 8 } else { 300 }
        Add-Tracking $h @{ title = $ti[0]; type = $ti[1]; length = $len; progress = $prog }
    }

    # review the finished ones
    $lib = Invoke-RestMethod -Uri "$ApiBase/library?status=finished&limit=50" -Headers $h
    $rated = 0
    foreach ($item in $lib.data) {
        if ($rated -ge 3) { break }
        Invoke-RestMethod -Method Post -Uri "$ApiBase/review" -Headers $hk -ContentType "application/json" `
            -Body (@{ mediaId = $item.mediaId; rating = (6, 7, 8, 9, 10 | Get-Random); comment = "Loved this one." } | ConvertTo-Json) | Out-Null
        $rated++
    }

    Write-Host "   + @$Name  ($($Titles.Count) tracked, $rated reviewed)"
    return $h
}

$aliceH = New-SeedUser -Name "alice" -Mail "alice@taletrack.dev" -Seed "Aneka" -Titles @(
    @("The Left Hand of Darkness", "Book", 100), @("Piranesi", "Book", 100),
    @("Dune", "Book", 60), @("Arrival", "Movie", 100), @("Severance", "Series", 100))

$bobH = New-SeedUser -Name "bob" -Mail "bob@taletrack.dev" -Seed "Felix" -Titles @(
    @("Project Hail Mary", "Book", 100), @("The Martian", "Book", 100),
    @("Blade Runner 2049", "Movie", 100), @("Foundation", "Series", 45), @("Neuromancer", "Book", 100))

$carolH = New-SeedUser -Name "carol" -Mail "carol@taletrack.dev" -Seed "Luna" -Titles @(
    @("Circe", "Book", 100), @("The Song of Achilles", "Book", 100), @("The Bear", "Series", 80))

function Send-FriendReq {
    param($FromHeaders, [string]$ToUsername)
    try {
        $found = Invoke-RestMethod -Uri "$ApiBase/users/search?username=$ToUsername" -Headers $FromHeaders
        if (-not $found.user) { return }
        Invoke-RestMethod -Method Post -Uri "$ApiBase/friends/requests" -Headers $FromHeaders `
            -ContentType "application/json" -Body (@{ userId = $found.user.userId } | ConvertTo-Json) | Out-Null
    } catch { }  # already pending / already friends -> ignore
}
function Accept-FriendReq {
    param($AsHeaders, [string]$FromUsername)
    $f = Invoke-RestMethod -Uri "$ApiBase/friends" -Headers $AsHeaders
    $req = $f.incoming | Where-Object { $_.username -eq $FromUsername } | Select-Object -First 1
    if ($req) {
        Invoke-RestMethod -Method Post -Uri "$ApiBase/friends/requests/$($req.requestId)" -Headers $AsHeaders `
            -ContentType "application/json" -Body (@{ accept = $true } | ConvertTo-Json) | Out-Null
    }
}

# demo <-> alice (accepted), bob -> demo (accepted), carol -> demo (still pending)
Send-FriendReq $auth   "alice";  Accept-FriendReq $aliceH "demo"
Send-FriendReq $bobH   "demo";   Accept-FriendReq $auth   "bob"
Send-FriendReq $aliceH "bob";    Accept-FriendReq $bobH   "alice"
Send-FriendReq $carolH "demo"    # left pending on purpose
Write-Host "   friendships: demo<->alice, demo<->bob, alice<->bob  |  carol->demo pending"

# demo: avatar + share everything (the migration defaulted existing rows to off)
$demoMe = Invoke-RestMethod -Uri "$ApiBase/user/me" -Headers $auth
Invoke-RestMethod -Method Put -Uri "$ApiBase/user/$($demoMe.data.id)" `
    -Headers @{ Authorization = "Bearer $($login.token)"; "X-Internal-Api-Key" = $internalKey } `
    -ContentType "application/json" `
    -Body (@{ avatarUrl = (& $avatar "Milo"); privacy = $shareAll } | ConvertTo-Json) | Out-Null

Write-Host ""
Write-Host "Done. Open http://localhost:8090/login"
Write-Host "  demo / alice / bob / carol   —  password: $Password"
