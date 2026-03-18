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
    $repoRoot = (Resolve-Path "$PSScriptRoot/..").Path

    $output = & "$repoRoot/eng/init-sdk.ps1" -RepoRoot $repoRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to initialize .NET SDK"
    }
    $dotnetInstallDir = ($output | Select-Object -Last 1).Trim()

    # Install additional runtimes from global.json. InitializeToolset resolves the Arcade
    # SDK but doesn't trigger restore. Build.proj delegates to Tools.proj for restore, which
    # imports InstallDotNetCore.targets to install additional runtimes from global.json.
    $globalJson = Get-Content (Join-Path $repoRoot 'global.json') | ConvertFrom-Json
    $arcadeSdkVersion = $globalJson.'msbuild-sdks'.'Microsoft.DotNet.Arcade.Sdk'
    # Need to use nested Join-Path calls to support Windows PowerShell, which doesn't support multiple paths in a single Join-Path call
    $toolsetLocationFile = Join-Path (Join-Path (Join-Path $repoRoot 'artifacts') 'toolset') "$arcadeSdkVersion.txt"
    $buildProj = Get-Content $toolsetLocationFile -TotalCount 1

    $dotnet = Join-Path $dotnetInstallDir 'dotnet'
    & $dotnet msbuild $buildProj /p:Restore=true /p:Build=false /p:RepoRoot="$repoRoot/" /clp:NoSummary
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore toolset"
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
