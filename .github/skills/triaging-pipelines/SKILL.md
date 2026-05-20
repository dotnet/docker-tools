---
name: triaging-pipelines
description: Triage recent Azure Pipelines runs and failures.
---

## Workflow

### Step 1: List recent builds

```shell
pwsh ./eng/docker-tools/skill-helpers/Get-RecentBuilds.ps1 -Organization dnceng -Project internal -Folder dotnet/docker-tools -Hours 24
```

Change the value of `-Hours` to list more or fewer builds.

### Step 2: Investigate failures

For any build whose state is `failed`, `partiallySucceeded`, or `canceled`, use the `investigating-pipeline` skill to view the build timeline and read failing task logs.

### Step 3: Correlate failures with recent pull requests and issues

To check recent issues, run `gh issue list --state all`.
To check recent pull requests, run `gh pr list --state all`.
Read the contents of issues and pull requests using the `gh` CLI if necessary.

Look for:

- A recent change that obviously caused the failure
- An existing known issue tracking this failure
- An open pull request that already addresses the failure
