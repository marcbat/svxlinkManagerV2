# Publie le site statique sur la web app hbspot (https://hbspot.azurewebsites.net).
# Prérequis : az CLI connecté à l'abonnement qui héberge le groupe Reseau-repeteur-romand.
param(
    [string]$ResourceGroup = 'Reseau-repeteur-romand',
    [string]$AppName = 'hbspot'
)

$ErrorActionPreference = 'Stop'

$source = $PSScriptRoot
$archive = Join-Path ([System.IO.Path]::GetTempPath()) "hbspot-site-$([guid]::NewGuid()).zip"

try {
    # Tout le dossier sauf ce script.
    $fichiers = Get-ChildItem $source -Exclude 'deploy.ps1'
    Compress-Archive -Path $fichiers.FullName -DestinationPath $archive

    az webapp deploy --resource-group $ResourceGroup --name $AppName --src-path $archive --type zip --clean true
    if ($LASTEXITCODE -ne 0) { throw "Échec du déploiement (code $LASTEXITCODE)." }

    Write-Host "Site publié sur https://$AppName.azurewebsites.net"
}
finally {
    Remove-Item $archive -ErrorAction SilentlyContinue
}
