#!/usr/bin/env pwsh
# =============================================================================
# publish-arm.ps1 — Publication SvxlinkManagerV2 pour Orange Pi (linux-arm)
#
# Workflow :
#   1. Démarre PostgreSQL local (Docker) pour la génération de code Wolverine
#   2. Exécute 'dotnet run -- codegen write' → génère les handlers pré-compilés
#   3. Arrête PostgreSQL local
#   4. Publie l'application (compile les handlers statiques → plus de Roslyn à runtime)
#   5. Optionnel : déploie sur l'Orange Pi via SSH
#
# Usage :
#   .\publish-arm.ps1                          # Complet (codegen + publish)
#   .\publish-arm.ps1 -SkipCodegen             # Publish seul (si handlers déjà générés)
#   .\publish-arm.ps1 -Deploy                  # Publish + déploiement SSH
#   .\publish-arm.ps1 -SkipCodegen -Deploy     # Publish + déploiement (sans codegen)
# =============================================================================
param(
    [switch]$SkipCodegen,
    [switch]$Deploy,
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
# Étape 1 & 2 : Wolverine codegen (génération des handlers statiques)
# =============================================================================
if (-not $SkipCodegen) {

    Write-Step "1/4  Démarrage PostgreSQL Docker (pour codegen Wolverine)"
    docker-compose up -d postgresql
    if ($LASTEXITCODE -ne 0) { throw "Impossible de démarrer le conteneur PostgreSQL" }

    Write-Warn "Attente de PostgreSQL (max 60s)..."
    $attempts = 0
    $health = ""
    do {
        Start-Sleep -Seconds 2
        $health = (docker inspect --format="{{.State.Health.Status}}" svxlinkmanager-postgresql 2>$null)
        $attempts++
    } while ($health -ne "healthy" -and $attempts -lt 30)

    if ($health -ne "healthy") {
        docker-compose stop postgresql | Out-Null
        throw "PostgreSQL non prêt après 60s (état: $health)"
    }
    Write-OK "PostgreSQL prêt."

    Write-Step "2/4  Génération du code Wolverine (handlers statiques)"
    Write-Warn "L'application démarre brièvement pour découvrir les handlers..."
    dotnet run --project $ProjectPath --environment Development -- codegen write
    $codegen_exit = $LASTEXITCODE

    Write-Step "Arrêt PostgreSQL Docker"
    docker-compose stop postgresql | Out-Null

    if ($codegen_exit -ne 0) {
        throw "Échec de la génération de code Wolverine (exit code: $codegen_exit)"
    }
    Write-OK "Code Wolverine généré dans Internal/Generated/"

} else {
    Write-Warn "Étapes 1-2 ignorées (--SkipCodegen). Les handlers pré-compilés existants seront utilisés."
}

# =============================================================================
# Étape 3 : Publication
# =============================================================================
Write-Step "3/4  Publication framework-dependent (linux-arm)"

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
Write-Step "Création de l'archive"
$zipPath = "publish/svxlinkmanagerv2-linux-arm.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$PublishDir/*" -DestinationPath $zipPath -CompressionLevel Optimal
Write-OK "Archive créée → $zipPath"

# =============================================================================
# Étape 4 : Déploiement SSH (optionnel)
# =============================================================================
if ($Deploy) {
    Write-Step "4/4  Déploiement sur $OrangePiHost"

    # Test de connexion
    $sshTest = ssh -o StrictHostKeyChecking=no -o BatchMode=yes -o ConnectTimeout=10 `
        $OrangePiHost "echo connected" 2>&1
    if ($sshTest -ne "connected") {
        throw "Impossible de se connecter à $OrangePiHost via SSH. Vérifiez que la clé SSH est installée."
    }
    Write-OK "SSH connecté."

    # Arrêter le service
    ssh -o StrictHostKeyChecking=no $OrangePiHost "systemctl stop svxlinkmanagerv2 2>/dev/null; echo service_stopped"

    # Copier les fichiers (exclure les configs Production déjà en place)
    Write-Warn "Copie des fichiers vers $RemoteAppDir..."
    scp -o StrictHostKeyChecking=no -r "$PublishDir/*" "${OrangePiHost}:${RemoteAppDir}/"
    if ($LASTEXITCODE -ne 0) { throw "Échec du SCP" }

    # Redémarrer le service
    ssh -o StrictHostKeyChecking=no $OrangePiHost "chmod +x $RemoteAppDir/SvxlinkManagerV2.Presentation && systemctl daemon-reload && systemctl start svxlinkmanagerv2 && sleep 5 && systemctl is-active svxlinkmanagerv2"
    Write-OK "Service redémarré sur $OrangePiHost"

} else {
    Write-Host ""
    Write-Warn "Déploiement ignoré. Pour déployer :"
    Write-Host "  .\publish-arm.ps1 -SkipCodegen -Deploy" -ForegroundColor White
    Write-Host ""
    Write-Host "Ou manuellement :" -ForegroundColor White
    Write-Host "  scp -r publish/linux-arm/* root@10.0.0.10:/opt/svxlinkmanagerv2/" -ForegroundColor White
    Write-Host "  ssh root@10.0.0.10 'systemctl restart svxlinkmanagerv2'" -ForegroundColor White
}

Write-Host ""
Write-Host "+-----------------------------------------------------------------+" -ForegroundColor Magenta
Write-Host "|  Publication terminée avec succes !                            |" -ForegroundColor Magenta
Write-Host "+-----------------------------------------------------------------+" -ForegroundColor Magenta
