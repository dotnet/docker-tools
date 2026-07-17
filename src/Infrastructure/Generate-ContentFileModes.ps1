# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

[CmdletBinding()]
param(
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$contentPath = Join-Path $PSScriptRoot 'Content'
$repoRoot = git -C $PSScriptRoot rev-parse --show-toplevel

if ($LASTEXITCODE -ne 0) {
    throw 'Unable to locate the Git repository root.'
}

$contentRepoPath = [IO.Path]::GetRelativePath($repoRoot, $contentPath).Replace('\', '/')
$entries = git -C $repoRoot ls-files --stage -- "$contentRepoPath"

if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read infrastructure file modes from the Git index.'
}

$fileModes = foreach ($entry in $entries) {
    if ($entry -notmatch '^(?<mode>\d+) \S+ \d+\t(?<path>.+)$') {
        throw "Unexpected Git index entry: '$entry'"
    }

    $relativePath = $Matches.path.Substring($contentRepoPath.Length + 1)
    "$($Matches.mode.Substring(2)) $relativePath"
}

$outputPath = Join-Path $PSScriptRoot 'ContentFileModes.txt'
$expectedContent = ($fileModes -join "`n") + "`n"

if ($Check) {
    $actualContent = [IO.File]::ReadAllText($outputPath).Replace("`r`n", "`n")
    if ($actualContent -ne $expectedContent) {
        throw "The generated file mode metadata is stale. Run '$PSCommandPath' to update it."
    }

    return
}

[IO.File]::WriteAllText($outputPath, $expectedContent, [Text.UTF8Encoding]::new($false))
