#!/usr/bin/env pwsh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

<#
.SYNOPSIS
Initializes the .NET SDK using Arcade tooling based on the version specified in global.json.

.DESCRIPTION
Uses the Arcade tools.sh (Linux) or tools.ps1 (Windows) InitializeDotNetCli function to
install the correct .NET SDK version. The SDK is installed to <RepoRoot>/.dotnet and added
to the PATH for subsequent commands.

#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$engCommonDir = Join-Path $RepoRoot 'eng' 'common'

$dotnetInstallScriptUrl = 'https://dot.net/v1/dotnet-install.ps1'
$dotnetInstallScriptPath = Join-Path $PSScriptRoot 'dotnet-install.ps1'

Invoke-WebRequest -Uri $dotnetInstallScriptUrl -OutFile $dotnetInstallScriptPath
Write-Host "Downloaded '$dotnetInstallScriptUrl' to '$dotnetInstallScriptPath'."

& $dotnetInstallScriptPath -Channel LTS -Architecture x64
