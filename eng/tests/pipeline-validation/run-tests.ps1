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
    [string]$Registry,
    [string]$RepoPrefix,
    [switch]$DisableHttpVerification,
    [switch]$PullImages,
    [string]$ImageInfoPath,
    [ValidateSet("functional", "pre-build")]
    [string[]]$TestCategories = @("functional")
)

# This script intentionally doesn't run any tests. It is to be used for pipeline validation.

# Ensure that a TestResults folder exists to allow test pipeline to target folder for copying.
$scriptDir = Split-Path -parent $PSCommandPath
New-Item -ItemType Directory $scriptDir/TestResults
