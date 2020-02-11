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
    $Architecture = "*"
)

Set-StrictMode -Version Latest

$imageBuilderArgs = "getBaseImageStatus --manifest $Manifest --architecture $Architecture"

& "$PSScriptRoot/Invoke-ImageBuilder.ps1" -ImageBuilderArgs $imageBuilderArgs
