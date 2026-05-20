#!/usr/bin/env pwsh
# Shows a focused summary of a pull request: metadata, reviews, issue-level comments,
# and inline review comments (which `gh pr view --json` does not expose).
# Requires `gh` CLI authenticated against the target repo.
#
# Usage:
#   ./Show-PullRequestComments.ps1 2100
#   ./Show-PullRequestComments.ps1 2100 -Repo dotnet/docker-tools

[CmdletBinding()]
param(
    [Parameter(Mandatory)][int] $PullRequest,
    [string] $Repo
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

function Write-BlockComment {
    param([string] $Text)
    if (-not $Text) { return }
    $Text.TrimEnd() -split "`n" | ForEach-Object { Write-Host "> $_" }
    Write-Host ""
}

# Fetch PR overview.
$viewArgs = @(
    "pr", "view", $PullRequest,
    "--json", "number,title,state,author,baseRefName,headRefName,isDraft,url,additions,deletions,changedFiles,reviewDecision,labels,reviews,comments"
)
if ($Repo) { $viewArgs += @("--repo", $Repo) }

$prJson = & gh @viewArgs
$pr = $prJson | ConvertFrom-Json

# Fetch inline review comments via REST API. `gh pr view --json` exposes review bodies
# but drops the inline diff comments attached to specific files/lines.
$apiPath = if ($Repo) {
    "repos/$Repo/pulls/$PullRequest/comments"
} else {
    "repos/{owner}/{repo}/pulls/$PullRequest/comments"
}

$inlineJson = & gh api --paginate $apiPath
$inline = $inlineJson | ConvertFrom-Json

# Render.
$title = if ($Repo) { "$Repo#$PullRequest" } else { "PR #$PullRequest" }
Write-Host "## $title - $($pr.title)"
Write-Host ""
Write-Host "- State: $($pr.state)$(if ($pr.isDraft) { ' (draft)' })"
Write-Host "- Author: $($pr.author.login)"
Write-Host "- Branch: $($pr.headRefName) -> $($pr.baseRefName)"
Write-Host "- Changes: +$($pr.additions)/-$($pr.deletions) ($($pr.changedFiles) files)"
Write-Host "- Review decision: $($pr.reviewDecision)"
if ($pr.labels) {
    Write-Host "- Labels: $(($pr.labels | ForEach-Object { $_.name }) -join ', ')"
}
Write-Host "- URL: $($pr.url)"
Write-Host ""

# Reviews (the Approve / Request changes / Comment submissions themselves).
Write-Host "### Reviews ($($pr.reviews.Count))"
Write-Host ""
if ($pr.reviews.Count -eq 0) {
    Write-Host "_None_"
} else {
    foreach ($review in $pr.reviews) {
        Write-Host "**$($review.author.login)** $($review.state) at $($review.submittedAt)"
        Write-BlockComment $review.body
    }
}
Write-Host ""

# Top-level (issue) comments on the PR conversation.
Write-Host "### Conversation comments ($($pr.comments.Count))"
Write-Host ""
if ($pr.comments.Count -eq 0) {
    Write-Host "_None_"
} else {
    foreach ($comment in $pr.comments) {
        Write-Host "**$($comment.author.login)** commented at $($comment.createdAt):"
        Write-BlockComment $comment.body
    }
}
Write-Host ""

# Inline review comments grouped by file and line.
Write-Host "### Inline review comments ($($inline.Count))"
Write-Host ""
if ($inline.Count -eq 0) {
    Write-Host "_None_"
} else {
    $grouped = $inline |
        Group-Object -Property { "$($_.path):$(if ($_.line) { $_.line } else { $_.original_line })" } |
        Sort-Object Name
    foreach ($group in $grouped) {
        $first = $group.Group[0]
        $line = if ($first.line) { $first.line } else { $first.original_line }
        Write-Host "#### $($first.path) (line $line)"
        Write-Host ""
        foreach ($comment in $group.Group | Sort-Object created_at) {
            Write-Host "**$($comment.user.login)** commented at $($comment.created_at):"
            Write-BlockComment $comment.body
        }
    }
}
