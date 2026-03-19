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

if ($env:OS -ne 'Windows_NT') {
    # On Linux, use the bash tools.sh (tools.ps1 uses dotnet-install.ps1
    # which doesn't work reliably on Linux)
    $initScript = "source '$engCommonDir/tools.sh'; InitializeDotNetCli true; InitializeToolset; echo `$_InitializeDotNetCli"
    $output = & bash -c $initScript
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to initialize .NET SDK via tools.sh"
    }
    # The last line of output is the dotnet install directory echoed by tools.sh
    ($output | Select-Object -Last 1).Trim()
} else {
    $ci = $true
    . (Join-Path $engCommonDir 'tools.ps1')
    $dotnetInstallDir = InitializeDotNetCli $true
    InitializeToolset
    $dotnetInstallDir
}
