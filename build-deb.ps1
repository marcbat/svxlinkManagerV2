#!/usr/bin/env pwsh
param(
    [string]$PackageVersion = "0.1.0",
    [string]$Configuration = "Release",
    [string]$Rid = "linux-arm",
    [string]$DebArchitecture = "armhf",
    [string]$ProjectPath = "src/SvxlinkManagerV2.Presentation/SvxlinkManagerV2.Presentation.csproj",
    [string]$PackageName = "svxlinkmanagerv2",
    [string]$OutputDir = "artifacts/deb",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $repoRoot

$publishDir = Join-Path $repoRoot ("artifacts/publish/{0}" -f $Rid)
$stagingRoot = Join-Path $repoRoot "artifacts/deb-staging"
$packageDirName = "{0}_{1}_{2}" -f $PackageName, $PackageVersion, $DebArchitecture
$packageRoot = Join-Path $stagingRoot $packageDirName
$appRoot = Join-Path $packageRoot ("opt/{0}" -f $PackageName)
$debianDir = Join-Path $packageRoot "DEBIAN"
$systemdDir = Join-Path $packageRoot "etc/systemd/system"
$helperPath = Join-Path $appRoot "install-update.sh"
$outputDirAbs = Join-Path $repoRoot $OutputDir
$debOutputPath = Join-Path $outputDirAbs ("{0}.deb" -f $packageDirName)

Write-Step "Preparation des dossiers"
if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingRoot | Out-Null
New-Item -ItemType Directory -Path $appRoot -Force | Out-Null
New-Item -ItemType Directory -Path $debianDir -Force | Out-Null
New-Item -ItemType Directory -Path $systemdDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDirAbs -Force | Out-Null

if (-not $SkipPublish) {
    Write-Step "dotnet publish ($Rid, framework-dependent)"
    dotnet publish $ProjectPath `
        -c $Configuration `
        -r $Rid `
        --no-self-contained `
        -p:InformationalVersion=$PackageVersion `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        throw "Echec du dotnet publish"
    }
}

if (-not (Test-Path (Join-Path $publishDir "SvxlinkManagerV2.Presentation.dll"))) {
    throw "Aucun publish detecte dans $publishDir. Lance sans -SkipPublish ou verifie le RID."
}

Write-Step "Assemblage du paquet Debian"
Copy-Item (Join-Path $publishDir "*") $appRoot -Recurse -Force

$devConfigPath = Join-Path $appRoot "appsettings.Development.json"
if (Test-Path $devConfigPath) {
    Remove-Item $devConfigPath -Force
}

New-Item -ItemType Directory -Path (Join-Path $appRoot "data") -Force | Out-Null

Copy-Item (Join-Path $repoRoot "deploy/systemd/svxlinkmanagerv2.service") $systemdDir -Force
Copy-Item (Join-Path $repoRoot "deploy/debian/postinst") (Join-Path $debianDir "postinst") -Force
Copy-Item (Join-Path $repoRoot "deploy/debian/prerm") (Join-Path $debianDir "prerm") -Force
Copy-Item (Join-Path $repoRoot "deploy/debian/postrm") (Join-Path $debianDir "postrm") -Force
Copy-Item (Join-Path $repoRoot "deploy/linux/install-update.sh") $helperPath -Force
Copy-Item (Join-Path $repoRoot "deploy/docker/dev-ca-hook.sh") (Join-Path $appRoot "dev-ca-hook.sh") -Force
Copy-Item (Join-Path $repoRoot "deploy/linux/setup-svxlink.sh") (Join-Path $appRoot "setup-svxlink.sh") -Force

$svxlinkConfigDest = Join-Path $appRoot "svxlink-config"
New-Item -ItemType Directory -Path $svxlinkConfigDest -Force | Out-Null
Copy-Item (Join-Path $repoRoot "svxlink-config/svxlink.conf") $svxlinkConfigDest -Force

$controlContent = @"
Package: $PackageName
Version: $PackageVersion
Section: misc
Priority: optional
Architecture: $DebArchitecture
Maintainer: SvxlinkManager Team
Depends: libc6 (>= 2.31), systemd, procps, alsa-utils, libsigc++-2.0-0v5, libgsm1, libpopt0, tcl8.6, libgcrypt20, libspeex1, libopus0, libcurl4, libasound2 | libasound2t64, libssl3 | libssl1.1, libjsoncpp25 | libjsoncpp1
Recommends: libttspico-utils, network-manager
Description: SvxlinkManagerV2 (framework-dependent) for Armbian armhf
 SvxlinkManagerV2 with systemd service for Orange Pi.
 .
 Depends couvre les outils invoques par l'application (pgrep/pkill via procps,
 amixer via alsa-utils, systemctl) et les bibliotheques des deux versions de
 SVXLink, y compris libssl et libjsoncpp requises par la 25.05.
 .
 Les bibliotheques dont le nom change selon la distribution sont declarees
 en alternatives : la cible Armbian Focal fournit libssl1.1 et libjsoncpp1,
 la ou Debian bookworm fournit libssl3 et libjsoncpp25. Sans alternative le
 paquet devient non installable sur l'une ou l'autre.
 .
 pico2wave (annonces vocales) et nmcli (Wi-Fi) sont en Recommends et non en
 Depends : libttspico-utils a ete retire de Debian et n'existe plus que sur
 Ubuntu, un Depends rendrait le paquet non installable sur un Armbian Debian.
 .
 Restent hors du contrat dpkg, faute de paquet installable :
 le runtime .NET 8 (linux-arm), absent des depots Microsoft pour armhf, et
 SVXLink (19.09.2 + 25.05), a compiler via setup-svxlink.sh.
"@

Set-Content -Path (Join-Path $debianDir "control") -Value $controlContent

Write-Step "Creation du .deb via Docker (dpkg-deb)"
$stagingRootRelative = [System.IO.Path]::GetRelativePath($repoRoot, $stagingRoot).Replace('\', '/')
$outputDirRelative = [System.IO.Path]::GetRelativePath($repoRoot, $outputDirAbs).Replace('\', '/')
$packagePathInContainer = "/workspace/$stagingRootRelative/$packageDirName"
$debPathInContainer = "/workspace/$outputDirRelative/$packageDirName.deb"

$dockerCmd = @(
    "set -e"
    "sed -i 's/\r$//' $packagePathInContainer/DEBIAN/postinst"
    "sed -i 's/\r$//' $packagePathInContainer/DEBIAN/prerm"
    "sed -i 's/\r$//' $packagePathInContainer/DEBIAN/postrm"
    "sed -i 's/\r$//' $packagePathInContainer/DEBIAN/control"
    "chmod 0755 $packagePathInContainer"
    "chmod 0755 $packagePathInContainer/DEBIAN"
    "chmod 0755 $packagePathInContainer/DEBIAN/postinst"
    "chmod 0755 $packagePathInContainer/DEBIAN/prerm"
    "chmod 0755 $packagePathInContainer/DEBIAN/postrm"
    "chmod 0644 $packagePathInContainer/DEBIAN/control"
    "chmod 0644 $packagePathInContainer/etc/systemd/system/svxlinkmanagerv2.service"
    "chmod 0755 $packagePathInContainer/opt/$PackageName/install-update.sh"
    "sed -i 's/\r$//' $packagePathInContainer/opt/$PackageName/dev-ca-hook.sh"
    "chmod 0755 $packagePathInContainer/opt/$PackageName/dev-ca-hook.sh"
    "sed -i 's/\r$//' $packagePathInContainer/opt/$PackageName/setup-svxlink.sh"
    "chmod 0755 $packagePathInContainer/opt/$PackageName/setup-svxlink.sh"
    "dpkg-deb --build $packagePathInContainer $debPathInContainer"
) -join "`n"

docker run --rm `
    -v "${repoRoot}:/workspace" `
    -w /workspace `
    debian:bookworm-slim `
    bash -lc $dockerCmd

if ($LASTEXITCODE -ne 0) {
    throw "Echec de creation du paquet .deb"
}

Write-Host "`nPaquet genere: $debOutputPath" -ForegroundColor Green
