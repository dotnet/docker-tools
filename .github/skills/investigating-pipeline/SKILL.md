---
name: investigating-pipeline
description: >-
  Diagnoses a single Azure Pipelines build. Shows the build timeline tree with stages, jobs, and
  task results, and retrieves task logs for debugging failures. Use when a user provides a build ID
  or Azure DevOps build URL and wants to understand what failed and why.
---

## Workflow

### Step 1: View the build timeline

```shell
eng/docker-tools/skill-helpers/Show-BuildTimeline.ps1 <org> <project> <buildId>
```

This prints a tree of stages, jobs, and tasks. By default only failing tasks are shown. Use `-ShowAllTasks` to see everything.

Each node includes a log ID (e.g., `Task #42`). Note the log IDs of failing tasks for the next step.

### Step 2: Read a failing task's log

```shell
eng/docker-tools/skill-helpers/Get-BuildLog.ps1 <org> <project> <buildId> <logId>
```

This prints the full log for a specific task. Use this to understand the root cause of a failure.
