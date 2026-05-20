---
name: investigating-pipeline
description: >-
  Diagnoses a single Azure Pipelines build. Shows the build timeline tree with stages, jobs, and
  task results, and retrieves task logs for debugging failures. Use when a user provides a build ID
  or Azure DevOps build URL and wants to understand what failed and why.
---

# Investigating Azure Pipelines builds

This document contains useful patterns for inspecting Azure Pipelines builds.

## How to view the build timeline

Use `Show-BuildTimeline.ps1` to view a build's timeline.
This includes all stages, jobs, and tasks along with their results.

```shell
pwsh ./eng/docker-tools/skill-helpers/Show-BuildTimeline.ps1 <org> <project> <buildId>
```

Each node includes a log ID (`Task #42` means log ID 42).

## How to read pipeline logs

First, get the log ID from the build timeline.
Then, use `Get-BuildLog.ps1` to print the full log:

```shell
pwsh ./eng/docker-tools/skill-helpers/Get-BuildLog.ps1 -Organization dnceng -Project internal -BuildId $buildId -LogId $logId
```
