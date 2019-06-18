[cmdletbinding(DefaultParameterSetName=$null)]
param(
    [Parameter(Mandatory = $true)][string]$RegistryName,
    [Parameter(Mandatory = $true)][string]$SubscriptionId,
    [switch]$WhatIf,
    [string]$ServicePrincipalName,
    [string]$ServicePrincipalPassword,
    [string]$ServicePrincipalTenant
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$suffixIndex = $RegistryName.IndexOf(".azurecr.io")
if ($suffixIndex -ge 0) {
    $RegistryName = $RegistryName.Substring(0, $suffixIndex)
}

if ($ServicePrincipalName) {
    az login `
        --subscription $SubscriptionId `
        --service-principal `
        --username $ServicePrincipalName `
        --password $ServicePrincipalPassword `
        --tenant $ServicePrincipalTenant
} else {
    az login --subscription $SubscriptionId
}

Write-Host "Querying registry '$RegistryName' for its repositories"
Write-Host ""
$repos = az acr repository list --name $RegistryName | ConvertFrom-Json
$filteredRepos = $repos | Where-Object { -not $_.StartsWith("public/") }

$deletedRepos = @()
foreach ($repoName in $filteredRepos) {
    Write-Host "Querying repository '$repoName'"
    $repo = az acr repository show --name $RegistryName --repository $repoName | ConvertFrom-Json
    $lastUpdateTime = [datetime]$repo.lastUpdateTime

    # If the repo was last updated more than X days ago, delete it
    if ($lastUpdateTime.AddDays(30) -lt (Get-Date)) {
        Write-Host "Deleting repository '$repoName'"
        if (-not $WhatIf) {
            az acr repository delete --name $RegistryName --repository $repoName    
        }

        $deletedRepos += $repoName
    }

    Write-Host ""
}

Write-Host "SUMMARY"
Write-Host "======="
Write-Host "Deleted repositories:"

foreach ($deletedRepo in $deletedRepos) {
    Write-Host $deletedRepo
}

Write-Host ""
Write-Host "Total repositories deleted: $($deletedRepos.Count)"
Write-Host "Total repositories remaining: $($repos.Count - $deletedRepos.Count)"
