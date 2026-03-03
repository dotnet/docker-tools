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

Push-Location $PSScriptRoot

try {
    if ($env:OS -ne 'Windows_NT') {
        # On Linux, use the bash tools.sh (tools.ps1 uses dotnet-install.ps1
        # which doesn't work reliably on Linux)
        $engCommonDir = (Resolve-Path "$PSScriptRoot/../eng/common").Path
        $initScript = "source '$engCommonDir/tools.sh'; InitializeDotNetCli true; echo `$_InitializeDotNetCli"
        $dotnetInstallDir = (& bash -c $initScript | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to initialize .NET SDK via tools.sh"
        }
    } else {
        . $PSScriptRoot/../eng/common/tools.ps1
        $dotnetInstallDir = InitializeDotNetCli $true
    }

    $cmd = "$dotnetInstallDir/dotnet test $PSScriptRoot/ImageBuilder.Tests/Microsoft.DotNet.ImageBuilder.Tests.csproj --logger:trx"

    Write-Output "Executing '$cmd'"
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) {
        throw "Failed: '$cmd'"
    }
}
finally {
    Pop-Location
}
