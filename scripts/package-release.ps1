<#
.SYNOPSIS
    Build + package RealisticFrag as a Forge-uploadable .7z archive.

.DESCRIPTION
    Produces `dist/RealisticFrag-x.y.z.7z` whose internal layout matches what users
    expect when dragging the archive onto `SPT Mods Installer.exe`:

        BepInEx/plugins/RealisticFrag.Client/
            RealisticFrag.Client.dll
        SPT/user/mods/RealisticFrag/
            RealisticFrag.dll
            config.json

    Drops the archive next to the .gitignore'd `dist/` folder so `git status` stays
    clean. Forge upload is manual — you drag the resulting .7z onto the listing.

.PARAMETER RepoRoot
    Defaults to this script's parent directory.

.PARAMETER Version
    Override the version string in the output filename. Defaults to the value parsed
    from RealisticFrag.csproj `<Version>`. Use this for prereleases:
        .\scripts\package-release.ps1 -Version 1.0.0-rc.1

.PARAMETER SevenZipPath
    Full path to 7z.exe. Defaults to auto-discovery — first checks PATH, then the
    standard Program Files install locations. Override only if your 7-Zip lives
    somewhere unusual.

.EXAMPLE
    .\scripts\package-release.ps1
    # Uses the version from .csproj. Output: dist/RealisticFrag-0.1.0.7z

.EXAMPLE
    .\scripts\package-release.ps1 -Version 1.0.0-rc.1
#>
[CmdletBinding()]
param(
    [string] $RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [string] $Version,
    [string] $SevenZipPath
)

$ErrorActionPreference = 'Stop'

function Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function OK  ([string]$msg) { Write-Host "    $msg" -ForegroundColor Green }

# 0. Resolve version
if (-not $Version) {
    [xml]$serverCsproj = Get-Content (Join-Path $RepoRoot 'RealisticFrag.csproj')
    $Version = $serverCsproj.Project.PropertyGroup.Version
    if (-not $Version) { throw "Could not parse <Version> from RealisticFrag.csproj" }
}
Step "Packaging RealisticFrag v$Version"

# 1. Verify all three projects build clean in Release config
Step 'Building server (Release)'
$srvOut = & dotnet build (Join-Path $RepoRoot 'RealisticFrag.csproj') --configuration Release --nologo 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host $srvOut; throw 'Server build failed' }
OK 'server OK'

Step 'Building client (Release)'
$cliOut = & dotnet build (Join-Path $RepoRoot 'client\RealisticFrag.Client.csproj') --configuration Release --nologo 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host $cliOut; throw 'Client build failed' }
OK 'client OK'

Step 'Running tests'
$testOut = & dotnet test (Join-Path $RepoRoot 'tests\RealisticFrag.Tests.csproj') --configuration Release --nologo 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host $testOut; throw 'Tests failed' }
OK 'tests OK'

# 2. Stage files in dist/staging/ matching SPT install layout
$dist    = Join-Path $RepoRoot 'dist'
$staging = Join-Path $dist 'staging'
if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
New-Item -ItemType Directory -Force -Path $staging | Out-Null

Step 'Staging release contents'
$serverDest = Join-Path $staging 'SPT\user\mods\RealisticFrag'
$clientDest = Join-Path $staging 'BepInEx\plugins\RealisticFrag.Client'
New-Item -ItemType Directory -Force -Path $serverDest, $clientDest | Out-Null

# Server DLL + config (output path from .csproj: bin\Release\RealisticFrag\)
$serverBin = Join-Path $RepoRoot 'bin\Release\RealisticFrag'
Copy-Item (Join-Path $serverBin 'RealisticFrag.dll') $serverDest -Force
Copy-Item (Join-Path $RepoRoot 'config.json')         $serverDest -Force

# Client DLL (output path: client\bin\Release\)
$clientBin = Join-Path $RepoRoot 'client\bin\Release'
Copy-Item (Join-Path $clientBin 'RealisticFrag.Client.dll') $clientDest -Force

# README for users who extract manually
Copy-Item (Join-Path $RepoRoot 'README.md')   (Join-Path $staging 'README.md')   -Force
Copy-Item (Join-Path $RepoRoot 'LICENSE')     (Join-Path $staging 'LICENSE')     -Force
Copy-Item (Join-Path $RepoRoot 'CHANGELOG.md') (Join-Path $staging 'CHANGELOG.md') -Force

OK "staged at $staging"

# 3. Create the .7z
$archiveName = "RealisticFrag-$Version.7z"
$archivePath = Join-Path $dist $archiveName
if (Test-Path $archivePath) { Remove-Item -Force $archivePath }

Step "Creating $archiveName"

# Locate 7-Zip. Order of preference:
#   1. -SevenZipPath parameter (explicit override)
#   2. 7z on the user's PATH
#   3. Common Windows install locations (both Program Files variants)
function Resolve-SevenZip {
    if ($SevenZipPath) {
        if (Test-Path -LiteralPath $SevenZipPath) { return $SevenZipPath }
        throw "7-Zip not found at -SevenZipPath: $SevenZipPath"
    }
    $onPath = Get-Command -Name '7z' -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $candidates = @(
        "$env:ProgramFiles\7-Zip\7z.exe",
        "${env:ProgramFiles(x86)}\7-Zip\7z.exe",
        "$env:LOCALAPPDATA\Programs\7-Zip\7z.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path -LiteralPath $c) { return $c }
    }
    throw @"
7-Zip not found. Tried PATH and these common locations:
$($candidates -join "`n")
Install 7-Zip from https://www.7-zip.org/ or pass -SevenZipPath <full path to 7z.exe>.
"@
}
$sevenZip = Resolve-SevenZip
OK "using 7-Zip at $sevenZip"

# Archive contents of staging (so the .7z root is BepInEx + SPT, not "staging\")
$out = & $sevenZip a -t7z -mx=9 $archivePath "$staging\*" 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host $out; throw '7z compression failed' }

$archiveInfo = Get-Item $archivePath
OK "wrote $($archiveInfo.FullName) ({0:N1} KB)" -f ($archiveInfo.Length / 1KB)

# 4. Cleanup staging (keep the .7z)
Remove-Item -Recurse -Force $staging

Write-Host ''
Write-Host "Release archive ready: $archivePath" -ForegroundColor Green
Write-Host "  Upload this to the Forge listing (the archive's internal layout matches SPT Mods Installer expectations)." -ForegroundColor Gray
