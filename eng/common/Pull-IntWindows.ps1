#!/usr/bin/env pwsh

<#
.SYNOPSIS
Pulls and tags a Windows image from the internal container registry.
#>

[cmdletbinding()]
param (
    # Name of the Windows image tag to pull (e.g. nanoserver:1909)
    [Parameter(Mandatory = $true)]
    [string]$ImageTag
)

$registry="msint.azurecr.io"

$appid = (az keyvault secret show -n registry-appid --vault-name msint-community --query value | Out-String).Trim().Trim('"')
$key = (az keyvault secret show -n registry-key --vault-name msint-community --query value | Out-String).Trim().Trim('"')

$key | docker login $registry -u $appid --password-stdin
try
{
    $privateTag = "$registry/private/windows/$ImageTag"
    docker pull $privateTag

    $publicTag = "mcr.microsoft.com/windows/$ImageTag"
    docker tag $privateTag $publicTag
}
finally
{
    docker logout $registry
}

Write-Host ""
Write-Host $publicTag
