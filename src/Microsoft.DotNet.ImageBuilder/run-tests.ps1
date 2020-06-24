#!/usr/bin/env pwsh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$dotnetInstallDir = "$PSScriptRoot/../../.dotnet"

& ../../eng/common/Install-DotNetSdk.ps1 $dotnetInstallDir

$cmd = "$DotnetInstallDir/dotnet test --logger:trx"

Write-Output "Executing '$cmd'"
Invoke-Expression $cmd
if ($LASTEXITCODE -ne 0) {
    throw "Failed: '$cmd'"
}
