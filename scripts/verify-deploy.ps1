<#
.SYNOPSIS
    Build, deploy, and smoke-test RealisticFrag against an SPT 4.0.13 install.

.DESCRIPTION
    End-to-end integration check, intended to be run after every code change:
      1. Builds the mod (`dotnet build` in the project directory).
      2. Copies the DLL + config.json into the SPT install's mods folder.
      3. Launches SPT.Server.exe headless.
      4. Polls the server log for "Server has started".
      5. Parses the RealisticFrag log line, asserts:
           - applied count > 0
           - applied count == number of overrides in config.json
           - "not found" count == 0
      6. Stops the server, returns exit code (0 = pass, 1 = fail).

    Uses the live SPT install at $SptRoot. The deployment overwrites any existing
    RealisticFrag mod folder. SVM and other already-installed mods continue to load
    normally during the test.

.PARAMETER ProjectRoot
    Path to the RealisticFrag project directory. Defaults to this script's parent.

.PARAMETER SptRoot
    Path to the SPT install. Defaults to C:\SPT.

.PARAMETER BuildConfig
    Build configuration (Debug or Release). Defaults to Debug.

.EXAMPLE
    .\scripts\verify-deploy.ps1
    # Default — builds Debug, deploys to C:\SPT, runs smoke test.

.EXAMPLE
    .\scripts\verify-deploy.ps1 -BuildConfig Release
    # Builds Release before deploying.
#>
[CmdletBinding()]
param(
    [string] $ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $SptRoot     = 'C:\SPT',
    [ValidateSet('Debug','Release')]
    [string] $BuildConfig = 'Debug'
)

$ErrorActionPreference = 'Stop'

function Write-Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok  ([string]$msg) { Write-Host "    $msg" -ForegroundColor Green }
function Write-Bad ([string]$msg) { Write-Host "    $msg" -ForegroundColor Red }

# Sanity: project + SPT install exist
if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot 'RealisticFrag.csproj'))) {
    Write-Bad "ProjectRoot '$ProjectRoot' has no RealisticFrag.csproj"; exit 1
}
if (-not (Test-Path -LiteralPath (Join-Path $SptRoot 'SPT\SPT.Server.exe'))) {
    Write-Bad "SptRoot '$SptRoot' has no SPT\SPT.Server.exe"; exit 1
}

# Sanity: server not already running
$running = Get-Process | Where-Object { $_.Name -eq 'SPT.Server' }
if ($running) {
    Write-Bad 'SPT.Server is already running. Stop it first.'; exit 1
}

# Step 1: build
Write-Step "Build ($BuildConfig)"
$buildOut = & dotnet build $ProjectRoot --configuration $BuildConfig --nologo 2>&1
$buildExit = $LASTEXITCODE
if ($buildExit -ne 0) {
    Write-Bad "dotnet build failed (exit $buildExit):"
    $buildOut | ForEach-Object { Write-Host "    $_" }
    exit 1
}
Write-Ok 'dotnet build OK'

# Step 2: deploy
Write-Step 'Deploy DLL + config to SPT mods folder'
$srcDll = Join-Path $ProjectRoot "bin\$BuildConfig\RealisticFrag\RealisticFrag.dll"
$srcCfg = Join-Path $ProjectRoot 'config.json'
if (-not (Test-Path -LiteralPath $srcDll)) {
    # Fall back to searching, in case the OutputPath structure differs
    $found = Get-ChildItem (Join-Path $ProjectRoot 'bin') -Recurse -Filter 'RealisticFrag.dll' | Select-Object -First 1
    if ($found) { $srcDll = $found.FullName } else { Write-Bad "Built DLL not found"; exit 1 }
}
$dstDir = Join-Path $SptRoot 'SPT\user\mods\RealisticFrag'
New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
Copy-Item -LiteralPath $srcDll -Destination $dstDir -Force
Copy-Item -LiteralPath $srcCfg -Destination $dstDir -Force
Write-Ok "Deployed to $dstDir"

# Step 3: count overrides in shipped config
$config = Get-Content -LiteralPath $srcCfg -Raw | ConvertFrom-Json
$expectedCount = ($config.AmmoOverrides.PSObject.Properties | Measure-Object).Count
Write-Ok "Config declares $expectedCount overrides"

# Step 4: clear stale logs and boot server
Write-Step 'Boot SPT.Server.exe'
$logRoot = Join-Path $SptRoot 'SPT\user\logs'
Get-ChildItem -LiteralPath $logRoot -Recurse -File -ErrorAction SilentlyContinue | Remove-Item -Force

Start-Process -FilePath (Join-Path $SptRoot 'SPT\SPT.Server.exe') -WorkingDirectory (Join-Path $SptRoot 'SPT') -PassThru | Out-Null

$startedAt = Get-Date
$started   = $false
$timeoutSec = 90
while (((Get-Date) - $startedAt).TotalSeconds -lt $timeoutSec) {
    Start-Sleep -Seconds 2
    $logs = Get-ChildItem (Join-Path $logRoot 'spt') -Filter '*.log' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
    if ($logs) {
        $tail = Get-Content -LiteralPath $logs[0].FullName -Tail 5 -ErrorAction SilentlyContinue
        if ($tail -match 'Server has started|happy playing') { $started = $true; break }
    }
}
$elapsed = ((Get-Date) - $startedAt).TotalSeconds

# Step 5: stop server
Get-Process | Where-Object { $_.Name -eq 'SPT.Server' } | Stop-Process -Force
Start-Sleep -Seconds 1

if (-not $started) {
    Write-Bad ("Server did not reach 'Server has started' within {0:N0}s" -f $timeoutSec); exit 1
}
Write-Ok ("Server started in {0:N1}s" -f $elapsed)

# Step 6: parse RealisticFrag lines
Write-Step 'Parse RealisticFrag log output'
$log = Get-ChildItem (Join-Path $logRoot 'spt') -Filter '*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $log) { Write-Bad 'No server log produced'; exit 1 }

$loadLine    = Select-String -LiteralPath $log.FullName -Pattern 'Mod: RealisticFrag version' | Select-Object -First 1
$summaryLine = Select-String -LiteralPath $log.FullName -Pattern '\[RealisticFrag\] applied overrides to' | Select-Object -First 1

if (-not $loadLine) {
    Write-Bad 'RealisticFrag did not appear in mod loader output'; exit 1
}
Write-Ok $loadLine.Line.Trim()

if (-not $summaryLine) {
    Write-Bad 'RealisticFrag did not log its summary line (OnLoad may have crashed)'
    # Print any error lines that mention RealisticFrag to aid diagnosis
    Select-String -LiteralPath $log.FullName -Pattern 'RealisticFrag' -CaseSensitive:$false | ForEach-Object { Write-Host "    $($_.Line)" }
    exit 1
}
Write-Ok $summaryLine.Line.Trim()

# Step 7: assertions on the summary line
if ($summaryLine.Line -match 'applied overrides to (\d+) ammo items \((\d+) not found\)') {
    $applied  = [int]$matches[1]
    $notFound = [int]$matches[2]

    $allOk = $true
    if ($notFound -ne 0) {
        Write-Bad "FAIL: $notFound override(s) referenced unknown template IDs"
        $allOk = $false
    }
    if ($applied -ne $expectedCount) {
        Write-Bad "FAIL: applied=$applied but config declared $expectedCount overrides"
        $allOk = $false
    }
    if ($allOk) {
        Write-Ok "PASS: applied=$applied notFound=0 expected=$expectedCount"
        exit 0
    } else {
        exit 1
    }
} else {
    Write-Bad "Could not parse summary line: $($summaryLine.Line)"
    exit 1
}
