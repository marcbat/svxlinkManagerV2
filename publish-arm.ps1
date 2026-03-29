#!/usr/bin/env pwsh
# =============================================================================
# publish-arm.ps1 — Publication SvxlinkManagerV2 pour Orange Pi (linux-arm)
#
# Workflow :
#   1. Publie l'application pour linux-arm (framework-dependent, .NET 8 requis sur cible)
#   2. Crée une archive ZIP
#   3. Optionnel : vérifie la config de l'Orange Pi puis déploie via SSH
#   4. Optionnel : supprime PostgreSQL de la cible (ancienne stack)
#
# Usage :
#   .\publish-arm.ps1                          # Publication seule
#   .\publish-arm.ps1 -Deploy                  # Publication + déploiement SSH
#   .\publish-arm.ps1 -Deploy -RemovePostgreSql # + suppression PostgreSQL sur la cible
# =============================================================================
param(
    [switch]$Deploy,
    [switch]$RemovePostgreSql,
    [string]$OrangePiHost = "root@10.0.0.10",
    [string]$RemoteAppDir = "/opt/svxlinkmanagerv2"
)

$ErrorActionPreference = "Stop"
$ProjectPath = "src/SvxlinkManagerV2.Presentation/SvxlinkManagerV2.Presentation.csproj"
$PublishDir  = "publish/linux-arm"

function Write-Step([string]$msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}
function Write-OK([string]$msg) {
    Write-Host "    [OK] $msg" -ForegroundColor Green
}
function Write-Warn([string]$msg) {
    Write-Host "    [!]  $msg" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "+-----------------------------------------------------------------+" -ForegroundColor Magenta
Write-Host "|  SvxlinkManagerV2 — Publication Orange Pi (linux-arm)          |" -ForegroundColor Magenta
Write-Host "+-----------------------------------------------------------------+" -ForegroundColor Magenta

# =============================================================================
# Étape 1 : Publication
# =============================================================================
Write-Step "1/3  Publication framework-dependent (linux-arm, net8.0)"

# Nettoyer l'ancien publish
if (Test-Path $PublishDir) {
    # Conserver seulement les fichiers de config Production et install.sh
    $filesToKeep = @("appsettings.Production.json", "svxlinkmanagerv2.service", "install.sh")
    Get-ChildItem $PublishDir -Exclude $filesToKeep | Remove-Item -Recurse -Force
}

dotnet publish $ProjectPath `
    -c Release `
    -r linux-arm `
    --no-self-contained `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) { throw "Échec du dotnet publish" }
Write-OK "Publish terminé → $PublishDir"

# Créer l'archive ZIP
Write-Step "2/3  Création de l'archive"
$zipPath = "publish/svxlinkmanagerv2-linux-arm.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$PublishDir/*" -DestinationPath $zipPath -CompressionLevel Optimal
Write-OK "Archive créée → $zipPath"

# =============================================================================
# Étape 3 : Déploiement SSH (optionnel)
# =============================================================================
if ($Deploy) {
    Write-Step "3/3  Déploiement sur $OrangePiHost"

    # Test de connexion
    $sshTest = ssh -o StrictHostKeyChecking=no -o BatchMode=yes -o ConnectTimeout=10 `
        $OrangePiHost "echo connected" 2>&1
    if ($sshTest -ne "connected") {
        throw "Impossible de se connecter à $OrangePiHost via SSH. Vérifiez que la clé SSH est installée."
    }
    Write-OK "SSH connecté."

    # Vérification runtime .NET de la cible
    $dotnetVersion = ssh -o StrictHostKeyChecking=no $OrangePiHost "dotnet --version" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet n'est pas disponible sur la cible. Installez .NET 8 runtime puis relancez."
    }
    Write-OK "Version .NET distante: $dotnetVersion"

    if ($dotnetVersion -notmatch "^8\\.") {
        Write-Warn "La cible n'est pas en .NET 8 (version détectée: $dotnetVersion)."
        Write-Warn "Le binaire publié est framework-dependent net8.0."
    }

    if ($RemovePostgreSql) {
        Write-Warn "Suppression de PostgreSQL sur la cible (ancienne stack)..."
        ssh -o StrictHostKeyChecking=no $OrangePiHost "set -e; systemctl stop postgresql 2>/dev/null || true; systemctl disable postgresql 2>/dev/null || true; apt-get purge -y postgresql postgresql-client postgresql-common || true; apt-get autoremove -y || true"
        if ($LASTEXITCODE -ne 0) {
            throw "Échec lors de la suppression de PostgreSQL sur la cible"
        }
        Write-OK "PostgreSQL supprimé de la cible"
    }

    # Arrêter le service
    ssh -o StrictHostKeyChecking=no $OrangePiHost "mkdir -p $RemoteAppDir && systemctl stop svxlinkmanagerv2 2>/dev/null || true; echo service_stopped"

    # Copier les fichiers (exclure les configs Production déjà en place)
    Write-Warn "Copie des fichiers vers $RemoteAppDir..."
    scp -o StrictHostKeyChecking=no -r "$PublishDir/*" "${OrangePiHost}:${RemoteAppDir}/"
    if ($LASTEXITCODE -ne 0) { throw "Échec du SCP" }

    # Redémarrer le service
    ssh -o StrictHostKeyChecking=no $OrangePiHost "chmod +x $RemoteAppDir/SvxlinkManagerV2.Presentation && systemctl daemon-reload && systemctl enable svxlinkmanagerv2 >/dev/null 2>&1 || true; systemctl restart svxlinkmanagerv2 && sleep 5 && systemctl is-active svxlinkmanagerv2"
    Write-OK "Service redémarré sur $OrangePiHost"

} else {
    Write-Host ""
    Write-Warn "Déploiement ignoré. Pour déployer :"
    Write-Host "  .\publish-arm.ps1 -Deploy -RemovePostgreSql" -ForegroundColor White
    Write-Host ""
    Write-Host "Ou manuellement :" -ForegroundColor White
    Write-Host "  scp -r publish/linux-arm/* root@10.0.0.10:/opt/svxlinkmanagerv2/" -ForegroundColor White
    Write-Host "  ssh root@10.0.0.10 'systemctl restart svxlinkmanagerv2'" -ForegroundColor White
}

Write-Host ""
Write-Host "+-----------------------------------------------------------------+" -ForegroundColor Magenta
Write-Host "|  Publication terminée avec succes !                            |" -ForegroundColor Magenta
Write-Host "+-----------------------------------------------------------------+" -ForegroundColor Magenta
