#!/usr/bin/env pwsh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[cmdletbinding()]
param(
    [string]$Version,
    [string]$Architecture,
    [string[]]$Paths,
    [string[]]$OSVersions,
    [string]$Registry,
    [string]$RepoPrefix,
    [switch]$DisableHttpVerification,
    [switch]$PullImages,
    [string]$ImageInfoPath,
    [string[]]$TestCategories = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$dotnetInstallDir = "$PSScriptRoot/../.dotnet"

Push-Location $PSScriptRoot

try {
    & ../eng/docker-tools/Install-DotNetSdk.ps1 $dotnetInstallDir

    $cmd = "$DotnetInstallDir/dotnet test $PSScriptRoot/ImageBuilder.Tests/Microsoft.DotNet.ImageBuilder.Tests.csproj --logger:trx"

    Write-Output "Executing '$cmd'"
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) {
        throw "Failed: '$cmd'"
    }
}
finally {
    Pop-Location
}
