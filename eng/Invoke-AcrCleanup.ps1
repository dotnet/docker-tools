#!/usr/bin/env pwsh

<#
    .SYNOPSIS
        Cleans up repos and images in ACR that are no longer needed.
#>

[cmdletbinding(DefaultParameterSetName=$null)]
param(
    # Name of the ACR
    [Parameter(Mandatory = $true)][string]$RegistryName,

    # Azure subscription ID
    [Parameter(Mandatory = $true)][string]$SubscriptionId,

    # Shows what would happen if deletions occurred but doesn't actually delete anything.
    [switch]$WhatIf,

    # Name of the ACR service principal
    [string]$ServicePrincipalName,

    # Password of the ACR service principal
    [string]$ServicePrincipalPassword,

    # Tenant of the ACR service principal
    [string]$ServicePrincipalTenant
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$suffixIndex = $RegistryName.IndexOf(".azurecr.io")
if ($suffixIndex -ge 0) {
    $RegistryName = $RegistryName.Substring(0, $suffixIndex)
}

az login `
    --service-principal `
    --username $ServicePrincipalName `
    --password $ServicePrincipalPassword `
    --tenant $ServicePrincipalTenant

az account set --subscription $SubscriptionId

Write-Host "Querying registry '$RegistryName' for its repositories"
Write-Host ""
$repos = az acr repository list --name $RegistryName | ConvertFrom-Json
$filteredRepos = $repos | Where-Object { -not $_.StartsWith("public/") -or $_.Contains("/core-nightly/") }

$deletedRepos = @()
$deletedImages = @()

foreach ($repoName in $filteredRepos) {
    Write-Host "Querying repository '$repoName'"
    $repo = az acr repository show --name $RegistryName --repository $repoName | ConvertFrom-Json

    if (-not $repoName.StartsWith("public/")) {
        $lastUpdateTime = [datetime]$repo.lastUpdateTime

        # If the repo was last updated more than X days ago, delete it
        if ($lastUpdateTime.AddDays(30) -lt (Get-Date)) {
            Write-Host "Deleting repository '$repoName'"
            if (-not $WhatIf) {
                az acr repository delete --name $RegistryName --repository $repoName -y
            }

            $deletedRepos += $repoName
        }
    }
    else {
        Write-Host "Querying manifests"
        $manifests = az acr repository show-manifests --name $RegistryName --repository $repoName | ConvertFrom-Json

        # Delete all of the untagged images
        $untaggedImages = $manifests | Where-Object { $_.tags.Count -eq 0 }
        foreach ($untaggedImage in $untaggedImages) {
            $imageId = "$repoName@$($untaggedImage.digest)"
            Write-Host "Deleting image '$imageId'"
            if (-not $WhatIf) {
                az acr repository delete --name $RegistryName --image $imageId -y
            }

            $deletedImages += $imageId
        }
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
Write-Host "Deleted images:"
foreach ($deletedImage in $deletedImages) {
    Write-Host $deletedImage
}

Write-Host ""
Write-Host "Total images deleted: $($deletedImages.Count)"
Write-Host "Total repositories deleted: $($deletedRepos.Count)"
Write-Host "Total repositories remaining: $($repos.Count - $deletedRepos.Count)"
