#!/usr/bin/env pwsh

<#
.SYNOPSIS
Lists ImageBuilder feature branches alongside the images they've published to MCR.

.DESCRIPTION
Feature branches are named feature/<name> and publish images tagged with <name> as a
prefix. See src/README.md. This joins the feature/* branches on a git remote with the
feature tags published to the registry, so a branch that hasn't published yet and an
image whose branch is gone are both visible.

Requires git and the oras CLI. MCR allows anonymous reads, so no login is needed.

.PARAMETER Remote
Git remote to query for feature/* branches.

.PARAMETER Repository
Registry repository that feature builds publish to.

.EXAMPLE
./Get-FeatureBuilds.ps1

.EXAMPLE
./Get-FeatureBuilds.ps1 -Remote internal | Format-Table -AutoSize
#>

[CmdletBinding()]
param(
    [string] $Remote = "origin",

    [string] $Repository = "mcr.microsoft.com/dotnet-buildtools/image-builder"
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

# A published feature image always includes the unsuffixed platform tags, e.g.
# "foobar-linux-amd64". Anchoring on a platform suffix is what separates a
# feature prefix from the official tags ("linux-amd64", "linux-amd64-3030923").
$PlatformTagPattern =
    '^(?<name>.+)-(linux-(amd64|arm64)|windowsservercore-[\w.]+-amd64|nanoserver-[\w.]+-amd64)$'

function Get-PublishedFeatureName([string[]] $Tags) {
    $Tags |
        ForEach-Object { if ($_ -match $PlatformTagPattern) { $Matches.name } } |
        Sort-Object -Unique
}

function ConvertTo-FeatureName([string] $Branch) {
    ($Branch -replace '^feature/', '') -replace '/', '-'
}

function Get-FeatureBranch([string] $Remote) {
    $branches = @{}
    foreach ($ref in git ls-remote --heads $Remote 'refs/heads/feature/*') {
        $branch = ($ref -split "`t")[1] -replace '^refs/heads/', ''
        $branches[(ConvertTo-FeatureName $branch)] = $branch
    }
    $branches
}

function Get-ImagePublishDate([string] $Repository, [string] $Tag) {
    try {
        $manifest = oras manifest fetch "${Repository}:${Tag}" | ConvertFrom-Json

        # Multi-platform tags point at an index; any child carries the build time.
        if ($manifest.manifests) {
            $manifest = oras manifest fetch "${Repository}@$($manifest.manifests[0].digest)" |
                ConvertFrom-Json
        }

        $config = oras blob fetch --output - "${Repository}@$($manifest.config.digest)" |
            ConvertFrom-Json

        if ($config.created) {
            ([datetime]$config.created).ToUniversalTime()
        }
    }
    catch {
        Write-Warning "Unable to read the publish date for ${Repository}:${Tag}: $_"
    }
}

$branches = Get-FeatureBranch -Remote $Remote
$published = Get-PublishedFeatureName -Tags (oras repo tags $Repository)

$features = @($branches.Keys) + @($published) | Sort-Object -Unique
if (-not $features) {
    Write-Warning "No feature branches or published feature images found."
    return
}

foreach ($feature in $features) {
    $isPublished = $published -contains $feature
    $publishDate = if ($isPublished) { Get-ImagePublishDate -Repository $Repository -Tag $feature }

    [PSCustomObject]@{
        Feature          = $feature
        Branch           = if ($branches[$feature]) { $branches[$feature] } else { "(missing)" }
        Image            = if ($isPublished) { "${Repository}:${feature}" } else { "Not yet published" }
        'Last Published' = if ($publishDate) { $publishDate.ToString('yyyy-MM-dd HH:mm') + " UTC" } else { "-" }
    }
}
