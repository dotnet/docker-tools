#!/usr/bin/env pwsh

<#
.SYNOPSIS
Outputs the status of external base images referenced in the Dockerfiles.
#>
[cmdletbinding()]
param(
    # Path to the manifest file to use
    [string]
    $Manifest = "manifest.json",

    # Architecture to filter Dockerfiles to
    [string]
    $Architecture = "*",

    # A value indicating whether to run the script continously
    [switch]
    $Continuous
)

Set-StrictMode -Version Latest

function RunCommand() {
    $imageBuilderArgs = "getBaseImageStatus --manifest $Manifest --architecture $Architecture"
    & "$PSScriptRoot/Invoke-ImageBuilder.ps1" -ImageBuilderArgs $imageBuilderArgs
}

if ($Continuous) {
    while ($true) {
        RunCommand

        # Pause before continuing so the user can scan through the results
        Start-Sleep -s 10
    }
}
else {
    RunCommand
}
