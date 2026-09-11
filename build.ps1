<#
    build.ps1 -- One-shot release pipeline for DeFocuser Lite (AppV2 + ASCOM driver).

    Usage:
        .\build.ps1 -Version 2.1.0
        .\build.ps1 -Version 2.1.0 -OutputDir "Output"
        .\build.ps1 -Version 2.1.0 -SkipBuild       # only repackages with current binaries
        .\build.ps1 -Version 2.1.0 -Configuration Release

    Steps:
      1. Patch AssemblyVersion/FileVersion in AppV2 + ASCOM_Driver
      2. Patch firmware __FIRMWARE_VERSION__ placeholder
      3. Build ASCOM_Driver  (Release | Any CPU)
      4. Build AppV2          (Release | x64)
      5. Compile firmware via arduino-cli, one .bin per board  -> Installer\*.bin
      6. Compile AppV2/Installer/Setup.iss with ISCC, injecting /DMyAppVersion
      7. Prune Installer/ to keep only the new -exe and -bin
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

$Driver       = Join-Path $RepoRoot 'Code\ASCOM_Driver\ASCOM.DeKoi.DeFocuserLite.csproj'
$App          = Join-Path $RepoRoot 'Code\FocuserApp\ASCOM.DeKoi.DeFocuserApp.csproj'
$Iss          = Join-Path $RepoRoot 'Code\FocuserApp\Installer\Setup.iss'
$FirmwareDir  = Join-Path $RepoRoot 'Code\Arduino_Firmware'
$FirmwareIno  = Join-Path $FirmwareDir 'Arduino_Firmware.ino'
$FirmwareBuildDir = Join-Path $RepoRoot 'build\firmware'

# One .bin per board revision. Id must match BOARD_ID in the .ino -- the hub
# compares the two to catch a wrong pick in its MCU dropdown before flashing.
# Order matters: hubs at or below 2.3.1 take the *first* firmware asset in the
# release, so the pre-rev build (what every existing unit runs) goes first.
$FirmwareVariants = @(
    @{ Id = 'esp32c3-old'; Define = 'ESP32C3_OLD'; Fqbn = 'esp32:esp32:XIAO_ESP32C3' },
    @{ Id = 'esp32c3';     Define = 'ESP32C3';     Fqbn = 'esp32:esp32:XIAO_ESP32C3' },
    @{ Id = 'esp32s3';     Define = 'ESP32S3';     Fqbn = 'esp32:esp32:XIAO_ESP32S3' }
)

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

function Find-ArduinoCli {
    $onPath = Get-Command arduino-cli -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }

    $candidates = @(
        "${env:ProgramFiles}\Arduino CLI\arduino-cli.exe",
        "${env:LOCALAPPDATA}\Programs\Arduino CLI\arduino-cli.exe",
        "${env:USERPROFILE}\bin\arduino-cli.exe"
    )
    foreach ($c in $candidates) { if (Test-Path -LiteralPath $c) { return $c } }
    throw "arduino-cli not found. Install from https://arduino.github.io/arduino-cli/ and ensure it's on PATH."
}

# ----- Patch firmware version literal -----
# In-place substitution mirrors AssemblyInfo handling. The .ino source contains
# 'v__FIRMWARE_VERSION__' which we replace with the real version before compile.
function Update-FirmwareVersion {
    param([string]$Path, [string]$NewVersion)

    if (-not (Test-Path -LiteralPath $Path)) { throw "Firmware source not found: $Path" }

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    $content   = [System.IO.File]::ReadAllText($Path, $utf8NoBom)

    # Replace either the placeholder OR a previously-stamped version
    # (so back-to-back builds with different versions work).
    $patched = [regex]::Replace(
        $content,
        '(?<=DeFocuser Lite Firmware v)(__FIRMWARE_VERSION__|\d+\.\d+(?:\.\d+)?)',
        $NewVersion)

    if ($patched -ne $content) {
        [System.IO.File]::WriteAllText($Path, $patched, $utf8NoBom)
        Write-Host "  patched firmware -> $NewVersion"
    } else {
        Write-Host "  (no firmware version change)"
    }
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

$script:MSBuild     = Find-MSBuild
$script:ISCC        = Find-ISCC
$script:ArduinoCli  = if ($SkipBuild) { $null } else { Find-ArduinoCli }
Write-Host "  msbuild:      $script:MSBuild"
Write-Host "  ISCC:         $script:ISCC"
if ($script:ArduinoCli) { Write-Host "  arduino-cli:  $script:ArduinoCli" }
Write-Host ""

# 1. Patch versions (assembly + firmware)
Write-Host "[1/5] Patching version metadata"
Update-AssemblyInfo -Path (Join-Path $RepoRoot 'Code\FocuserApp\Properties\AssemblyInfo.cs')   -NewVersion $AssemblyVersion
Update-AssemblyInfo -Path (Join-Path $RepoRoot 'Code\ASCOM_Driver\Properties\AssemblyInfo.cs') -NewVersion $AssemblyVersion
Update-FirmwareVersion -Path $FirmwareIno -NewVersion $InnoVersion

# 2. Build driver + app
if ($SkipBuild) {
    Write-Host "[2/5] Skipping build (-SkipBuild)"
} else {
    Write-Host "[2/5] Building binaries"
    Invoke-MSBuild -Project $Driver -Config $Configuration -Platform 'AnyCPU'
    Invoke-MSBuild -Project $App    -Config $Configuration -Platform 'x64'
}

# 3. Sanity-check expected outputs
$expectedDll = Join-Path $RepoRoot "Code\ASCOM_Driver\bin\$Configuration\ASCOM.DeKoi.DeFocuserLite.dll"
$expectedExe = Join-Path $RepoRoot "Code\FocuserApp\bin\x64\$Configuration\ASCOM.DeKoi.DeFocuserApp.exe"
foreach ($f in @($expectedDll, $expectedExe)) {
    if (-not (Test-Path -LiteralPath $f)) { throw "Build artifact missing: $f" }
}

# Ensure output dir exists before firmware compile copies into it
if (-not (Test-Path -LiteralPath $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# 4. Compile firmware, one .bin per board revision
$FirmwareBinNames = $FirmwareVariants | ForEach-Object { "DeFocuser-Lite-Firmware-$InnoVersion-$($_.Id).bin" }

if ($SkipBuild) {
    Write-Host "[3/5] Skipping firmware compile (-SkipBuild)"
} else {
    Write-Host "[3/5] Compiling firmware ($($FirmwareVariants.Count) variants)"
    if (Test-Path -LiteralPath $FirmwareBuildDir) {
        Remove-Item -LiteralPath $FirmwareBuildDir -Recurse -Force
    }

    & $script:ArduinoCli core install esp32:esp32 2>&1 | Out-Host

    foreach ($variant in $FirmwareVariants) {
        $variantDir = Join-Path $FirmwareBuildDir $variant.Id
        New-Item -ItemType Directory -Path $variantDir -Force | Out-Null

        # -DMCU= selects the pinout at compile time, so a release build never
        # edits the .ino. The #ifndef guard in the sketch keeps a bare
        # `arduino-cli compile` working for local dev.
        Write-Host "  compiling $($variant.Id) [$($variant.Define) | $($variant.Fqbn)]"
        & $script:ArduinoCli compile `
            --fqbn $variant.Fqbn `
            --build-property "compiler.cpp.extra_flags=-DMCU=$($variant.Define)" `
            --output-dir $variantDir $FirmwareDir 2>&1 | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "arduino-cli compile failed for $($variant.Id) (exit $LASTEXITCODE)" }

        $mergedBin = Get-ChildItem -LiteralPath $variantDir -Filter '*.merged.bin' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $mergedBin) {
            # Fallback to the legacy single-app .bin (older arduino-esp32 cores)
            $mergedBin = Get-ChildItem -LiteralPath $variantDir -Filter '*.ino.bin' -ErrorAction SilentlyContinue | Select-Object -First 1
        }
        if (-not $mergedBin) { throw "Firmware .bin not found in $variantDir" }

        $dest = Join-Path $OutputPath "DeFocuser-Lite-Firmware-$InnoVersion-$($variant.Id).bin"
        Copy-Item -LiteralPath $mergedBin.FullName -Destination $dest -Force

        # The .ino bakes BOARD_ID into the image; if it doesn't match the name
        # we're about to ship, the -D override silently didn't apply.
        $bytes = [System.IO.File]::ReadAllBytes($dest)
        $text  = [System.Text.Encoding]::ASCII.GetString($bytes)
        if ($text -notmatch [regex]::Escape("RESULT:FOCUSER:BOARD:")) {
            throw "$($variant.Id): firmware image has no BOARD_ID marker"
        }
        if ($text -notmatch [regex]::Escape($variant.Id)) {
            throw "$($variant.Id): BOARD_ID missing from image -- -DMCU override did not apply"
        }
        # "esp32c3" is a substring of "esp32c3-old", so presence alone can't tell
        # those two apart. Any *other* id that isn't a substring of this one must
        # be absent, which pins the image to exactly one variant.
        foreach ($other in $FirmwareVariants) {
            if ($other.Id -eq $variant.Id) { continue }
            if ($variant.Id.Contains($other.Id)) { continue }
            if ($text -match [regex]::Escape($other.Id)) {
                throw "$($variant.Id): image also carries BOARD_ID '$($other.Id)' -- wrong variant compiled"
            }
        }

        Write-Host "  firmware -> $dest"
    }

    # Clean both build caches (central output-dir and the cache arduino-cli
    # drops next to the .ino) now that the artifacts are safely in Installer/.
    foreach ($dir in @($FirmwareBuildDir, (Join-Path $FirmwareDir 'build'))) {
        if (Test-Path -LiteralPath $dir) {
            Remove-Item -LiteralPath $dir -Recurse -Force
            Write-Host "  cleaned $dir"
        }
    }

    # Also drop the parent build/ folder at the repo root if it's empty now.
    $rootBuild = Join-Path $RepoRoot 'build'
    if ((Test-Path -LiteralPath $rootBuild) -and -not (Get-ChildItem -LiteralPath $rootBuild -Force)) {
        Remove-Item -LiteralPath $rootBuild -Force
        Write-Host "  cleaned $rootBuild"
    }
}

# 5. Compile installer (ISCC /O overrides [Setup] OutputDir, /F overrides OutputBaseFilename)
Write-Host "[4/5] Compiling installer"
& $script:ISCC "/DMyAppVersion=$InnoVersion" "/O$OutputPath" $Iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)" }

# 6. Prune older artifacts so Installer/ only carries the current release pair
Write-Host "[5/5] Pruning old artifacts"
$keepInstaller = "DeKoi DeFocuser Lite Setup-$InnoVersion.exe"
$keepFirmware  = $FirmwareBinNames

Get-ChildItem -LiteralPath $OutputPath -Filter '*.exe' |
    Where-Object { $_.Name -like 'DeKoi DeFocuser Lite Setup-*' -and $_.Name -ne $keepInstaller } |
    ForEach-Object {
        Write-Host "  pruned $($_.Name)"
        Remove-Item -LiteralPath $_.FullName -Force
    }

Get-ChildItem -LiteralPath $OutputPath -Filter '*.bin' |
    Where-Object { $_.Name -like 'DeFocuser-Lite-Firmware-*' -and $keepFirmware -notcontains $_.Name } |
    ForEach-Object {
        Write-Host "  pruned $($_.Name)"
        Remove-Item -LiteralPath $_.FullName -Force
    }

Write-Host ""
Write-Host "Output:"
Get-ChildItem -LiteralPath $OutputPath |
    Where-Object { $_.Name -eq $keepInstaller -or $keepFirmware -contains $_.Name } |
    ForEach-Object { Write-Host "  -> $($_.FullName)" }

Write-Host ""
Write-Host "Done."
