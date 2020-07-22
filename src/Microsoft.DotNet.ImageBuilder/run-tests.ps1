#!/usr/bin/env pwsh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[cmdletbinding()]
param(
    [string]$Version,
    [string]$Architecture,
    [string]$OS,
    [ValidateSet("functional", "pre-build")]
    [string[]]$TestCategories = @("functional")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$dotnetInstallDir = "$PSScriptRoot/../../.dotnet"

Push-Location $PSScriptRoot

if ($TestCategories.Contains("pre-build")) {
    Write-Output "There are no pre-build tests"
}

if ($TestCategories.Contains("functional")) {
    try {
        & ../../eng/common/Install-DotNetSdk.ps1 $dotnetInstallDir

        $cmd = "$DotnetInstallDir/dotnet test --logger:trx"

        Write-Output "Executing '$cmd'"
        Invoke-Expression $cmd
        if ($LASTEXITCODE -ne 0) {
            throw "Failed: '$cmd'"
        }
    }
    finally {
        Pop-Location
    }
}
