[cmdletbinding(DefaultParameterSetName=$null)]
param(
    [Parameter(Mandatory = $true)]
    [string]
    $RegistryName,

    [Parameter(Mandatory = $true)]
    [string]
    $SubscriptionId,

    [switch]
    $WhatIf,

    [string]
    $ServicePrincipalName,

    [string]
    $ServicePrincipalPassword,

    [string]
    $ServicePrincipalTenant
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

$totalCount = 0
$intDeleteCount = 0
foreach ($repoName in $repos) {
    $totalCount++
    Write-Host "Querying repository '$repoName'"
    $repo = az acr repository show --name $RegistryName --repository $repoName | ConvertFrom-Json
    $lastUpdateTime = [datetime]$repo.lastUpdateTime

    # If the repo was last updated more than X days ago, delete it
    if ($lastUpdateTime.AddDays(30) -lt (Get-Date)) {
        $intDeleteCount++
        Write-Host "Deleting repository '$repoName'"
        if (-not $WhatIf) {
            az acr repository delete --name $RegistryName --repository $repoName    
        }
    }

    Write-Host ""
}

Write-Host "Total: $totalCount"
Write-Host "Deleted: $intDeleteCount"
