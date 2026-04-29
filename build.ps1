<#
    build.ps1 -- One-shot release pipeline for DeFocuser Lite (AppV2 + ASCOM driver).

    Usage:
        .\build.ps1 -Version 2.1.0
        .\build.ps1 -Version 2.1.0 -OutputDir "Output"
        .\build.ps1 -Version 2.1.0 -SkipBuild       # only repackages with current binaries
        .\build.ps1 -Version 2.1.0 -Configuration Release

    Steps:
      1. Patch AssemblyVersion/FileVersion in AppV2 + ASCOM_Driver
      2. Build ASCOM_Driver  (Release | Any CPU)
      3. Build AppV2          (Release | x64)
      4. Compile AppV2/Installer/Setup.iss with ISCC, injecting /DMyAppVersion
      5. Move installer to -OutputDir
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version,

    [string]$OutputDir = "Installer",

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$RepoRoot = $PSScriptRoot

# Normalize version: AssemblyVersion needs four parts.
$AssemblyVersion = if ($Version.Split('.').Count -eq 3) { "$Version.0" } else { $Version }
# Inno MyAppVersion uses three parts (drop trailing .0 if present)
$InnoVersion = ($AssemblyVersion -replace '\.0$', '')

$Driver = Join-Path $RepoRoot 'Code\ASCOM_Driver\ASCOM.DeKoi.DeFocuserLite.csproj'
$App    = Join-Path $RepoRoot 'Code\FocuserApp\ASCOM.DeKoi.DeFocuserApp.csproj'
$Iss    = Join-Path $RepoRoot 'Code\FocuserApp\Installer\Setup.iss'

$OutputPath = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $RepoRoot $OutputDir }

# ----- Locate toolchain -----
function Find-MSBuild {
    $candidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($c in $candidates) { if (Test-Path -LiteralPath $c) { return $c } }
    throw "MSBuild not found. Install Visual Studio 2019/2022 or Build Tools."
}

function Find-ISCC {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path -LiteralPath $c) { return $c } }
    throw "ISCC.exe not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php"
}

# ----- Patch AssemblyInfo.cs -----
# Use raw byte/UTF8 IO so PowerShell 5.1 doesn't mangle non-ASCII glyphs
# (e.g. (c) symbol) by reading them as Windows-1252.
function Update-AssemblyInfo {
    param([string]$Path, [string]$NewVersion)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "AssemblyInfo not found: $Path"
    }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $content   = [System.IO.File]::ReadAllText($Path, $utf8NoBom)
    $patched   = $content `
        -replace '(?<=AssemblyVersion\(")[^"]+(?="\))', $NewVersion `
        -replace '(?<=AssemblyFileVersion\(")[^"]+(?="\))', $NewVersion

    if ($patched -ne $content) {
        [System.IO.File]::WriteAllText($Path, $patched, $utf8NoBom)
        Write-Host "  patched $([System.IO.Path]::GetFileName((Split-Path $Path -Parent))) -> $NewVersion"
    } else {
        Write-Host "  (no version change for $Path)"
    }
}

# ----- Run msbuild -----
function Invoke-MSBuild {
    param([string]$Project, [string]$Config, [string]$Platform)

    Write-Host "-> Building $([System.IO.Path]::GetFileName($Project))  [$Config | $Platform]"
    # RegisterForComInterop=false: skip regasm during build (needs admin).
    # The installer handles COM registration on the target machine.
    & $script:MSBuild $Project `
        -t:Rebuild `
        -p:Configuration=$Config `
        -p:Platform=$Platform `
        -p:RegisterForComInterop=false `
        -verbosity:minimal -nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $Project ($Config | $Platform)" }
}

# ===========================================================================
# Pipeline
# ===========================================================================
Write-Host ""
Write-Host "DeFocuser Lite -- release build"
Write-Host "  Version:      $InnoVersion (assembly $AssemblyVersion)"
Write-Host "  Output:       $OutputPath"
Write-Host "  Config:       $Configuration"
Write-Host ""

# Validate inputs exist
foreach ($p in @($Driver, $App, $Iss)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Required file missing: $p" }
}

$script:MSBuild = Find-MSBuild
$script:ISCC    = Find-ISCC
Write-Host "  msbuild:      $script:MSBuild"
Write-Host "  ISCC:         $script:ISCC"
Write-Host ""

# 1. Patch versions
Write-Host "[1/4] Patching AssemblyInfo files"
Update-AssemblyInfo -Path (Join-Path $RepoRoot 'Code\FocuserApp\Properties\AssemblyInfo.cs')   -NewVersion $AssemblyVersion
Update-AssemblyInfo -Path (Join-Path $RepoRoot 'Code\ASCOM_Driver\Properties\AssemblyInfo.cs') -NewVersion $AssemblyVersion

# 2. Build driver + app
if ($SkipBuild) {
    Write-Host "[2/4] Skipping build (-SkipBuild)"
} else {
    Write-Host "[2/4] Building binaries"
    Invoke-MSBuild -Project $Driver -Config $Configuration -Platform 'AnyCPU'
    Invoke-MSBuild -Project $App    -Config $Configuration -Platform 'x64'
}

# 3. Sanity-check expected outputs
$expectedDll = Join-Path $RepoRoot "Code\ASCOM_Driver\bin\$Configuration\ASCOM.DeKoi.DeFocuserLite.dll"
$expectedExe = Join-Path $RepoRoot "Code\FocuserApp\bin\x64\$Configuration\ASCOM.DeKoi.DeFocuserApp.exe"
foreach ($f in @($expectedDll, $expectedExe)) {
    if (-not (Test-Path -LiteralPath $f)) { throw "Build artifact missing: $f" }
}

# 4. Compile installer (ISCC /O overrides [Setup] OutputDir, /F overrides OutputBaseFilename)
Write-Host "[3/4] Compiling installer"
if (-not (Test-Path -LiteralPath $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

& $script:ISCC "/DMyAppVersion=$InnoVersion" "/O$OutputPath" $Iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)" }

# 5. List produced installers
Write-Host "[4/4] Output:"
Get-ChildItem -LiteralPath $OutputPath -Filter '*.exe' |
    Where-Object { $_.Name -like "*$InnoVersion*" } |
    ForEach-Object { Write-Host "  -> $($_.FullName)" }

Write-Host ""
Write-Host "Done."
