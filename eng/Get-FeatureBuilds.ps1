#!/usr/bin/env pwsh
# Lists ImageBuilder feature branches alongside the images they've published to MCR.
# Feature branches are named feature/<name> and publish images tagged with <name>
# as a prefix. See src/README.md.
#
# Usage:
#   ./Get-FeatureBuilds.ps1
#   ./Get-FeatureBuilds.ps1 -Remote internal
#   ./Get-FeatureBuilds.ps1 | Format-Table -AutoSize
#
# Requires git and the oras CLI. MCR allows anonymous reads, so no login is needed.

[CmdletBinding()]
param(
    # Git remote to query for feature/* branches.
    [string] $Remote = "origin",

    [string] $Repository = "mcr.microsoft.com/dotnet-buildtools/image-builder",

    # Run the parsing self-check instead of querying git and the registry.
    [switch] $SelfTest
)

$ErrorActionPreference = "Stop"

# A published feature image always includes the unsuffixed platform tags, e.g.
# "foobar-linux-amd64". Anchoring on a platform suffix is what separates a
# feature prefix from the official tags ("linux-amd64", "linux-amd64-3030923").
$PlatformTagPattern =
    '^(?<name>.+)-(linux-(amd64|arm64)|windowsservercore-[\w.]+-amd64|nanoserver-[\w.]+-amd64)$'

function Get-FeatureNameFromTags {
    param([string[]] $Tags)

    $Tags |
        ForEach-Object { if ($_ -match $PlatformTagPattern) { $Matches.name } } |
        Sort-Object -Unique
}

function ConvertTo-FeatureName {
    param([string] $Branch)

    ($Branch -replace '^feature/', '') -replace '/', '-'
}

function Invoke-Native {
    param([scriptblock] $Command)

    $output = & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Command"
    }
    $output
}

function Get-FeatureBranch {
    param([string] $Remote)

    $branches = @{}
    $refs = Invoke-Native { git ls-remote --heads $Remote 'refs/heads/feature/*' }
    foreach ($ref in $refs) {
        $branch = ($ref -split "`t")[1] -replace '^refs/heads/', ''
        $branches[(ConvertTo-FeatureName $branch)] = $branch
    }
    $branches
}

function Get-ImagePublishDate {
    param([string] $Repository, [string] $Tag)

    try {
        $manifest = Invoke-Native { oras manifest fetch "${Repository}:${Tag}" } | ConvertFrom-Json

        # Multi-platform tags point at an index; any child carries the build time.
        if ($manifest.manifests) {
            $manifest = Invoke-Native {
                oras manifest fetch "${Repository}@$($manifest.manifests[0].digest)"
            } | ConvertFrom-Json
        }

        $config = Invoke-Native {
            oras blob fetch --output - "${Repository}@$($manifest.config.digest)"
        } | ConvertFrom-Json

        if ($config.created) {
            ([datetime]$config.created).ToUniversalTime()
        }
    }
    catch {
        Write-Warning "Unable to read the publish date for ${Repository}:${Tag}: $_"
    }
}

function New-FeatureRow {
    param(
        [string] $Feature,
        [string] $Branch,
        [string] $Image,
        # A [datetime], or $null when the feature has never published.
        [object] $PublishDate
    )

    [PSCustomObject]@{
        Feature          = $Feature
        Branch           = if ($Branch) { $Branch } else { "(missing)" }
        Image            = if ($Image) { $Image } else { "Not yet published" }
        'Last Published' = if ($PublishDate) { ([datetime]$PublishDate).ToString('yyyy-MM-dd HH:mm') + " UTC" } else { "-" }
    }
}

if ($SelfTest) {
    $names = Get-FeatureNameFromTags @(
        'latest', 'linux-amd64', 'linux-amd64-3030923', 'nanoserver-ltsc2022-amd64-3030923',
        'foobar', 'foobar-3030999', 'foobar-linux-amd64', 'foobar-linux-amd64-3030999',
        'foobar-nanoserver-ltsc2022-amd64', 'my-feature-linux-arm64')
    if ("$names" -ne 'foobar my-feature') {
        throw "Expected 'foobar my-feature' from the sample tags, got '$names'"
    }
    if ((Get-FeatureNameFromTags @('latest', 'linux-amd64-3030923')).Count -ne 0) {
        throw "Official tags must not be reported as feature branches"
    }
    if ((ConvertTo-FeatureName 'feature/foo/bar') -ne 'foo-bar') {
        throw "Slashes in a branch name must be flattened"
    }

    $missing = New-FeatureRow -Feature 'foobar'
    if ($missing.Branch -ne '(missing)' -or $missing.Image -ne 'Not yet published' -or
        $missing.'Last Published' -ne '-') {
        throw "Expected placeholders for a feature with no branch and no image, got '$missing'"
    }

    $full = New-FeatureRow -Feature 'foobar' -Branch 'feature/foobar' -Image 'repo:foobar' `
        -PublishDate ([datetime]::Parse('2026-07-24T22:12:40Z')).ToUniversalTime()
    if ($full.'Last Published' -ne '2026-07-24 22:12 UTC') {
        throw "Expected a UTC publish date, got '$($full.'Last Published')'"
    }

    Write-Host "Self-check passed."
    return
}

$branches = Get-FeatureBranch -Remote $Remote
$published = Get-FeatureNameFromTags (Invoke-Native { oras repo tags $Repository })

$features = @($branches.Keys) + @($published) | Sort-Object -Unique
if (-not $features) {
    Write-Host "No feature branches or published feature images found."
    return
}

foreach ($feature in $features) {
    $isPublished = $published -contains $feature
    $publishDate = if ($isPublished) { Get-ImagePublishDate -Repository $Repository -Tag $feature }

    New-FeatureRow `
        -Feature $feature `
        -Branch $branches[$feature] `
        -Image $(if ($isPublished) { "${Repository}:${feature}" }) `
        -PublishDate $publishDate
}
