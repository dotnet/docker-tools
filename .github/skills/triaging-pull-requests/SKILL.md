---
name: triaging-pull-requests
description: Triage open pull requests into actionable categories.
user-invocable: true
disable-model-invocation: true
---

## Workflow

### Step 1: List open pull requests

List open pull requests by running `gh pr list`.

### Step 2: Categorize pull requests

Read the contents of pull requests using `gh pr view`.
For any pull request that needs deeper status, review, comment, or CI context, use the `investigating-pull-request` skill.

Categorize each pull request into one of the following buckets:

- Ready to merge
- Needs review
- PR validation failing
- Needs follow-up
- Draft

### Step 3: Investigate failures

For failing pull requests, use the `investigating-pull-request` skill to gather their full context and check their CI status.

For pull requests with CI failures, use the `investigating-pipeline` skill with the failing build ID to read task logs and identify root causes.

### Step 4: Correlate failures with recent pull requests and issues

To check recent issues, run `gh issue list --state all`.
To check recently merged pull requests, run `gh pr list --state merged`.

Look for:

- A recent change that obviously caused the failure
- An existing known issue tracking this failure
- An open pull request that already addresses the failure

### Step 5: Categorize and present results

Using everything you've learned, place each pull request into **one** of these
categories in this priority order:

1. **Ready to Merge** — Approved, CI passing, no merge conflicts
2. **Needs Review** — The current user is a requested reviewer
3. **Needs Author Action** — Changes requested, CI failing, or merge conflicts
4. **Stale** — No updates in 7+ days and not ready to merge

Use your judgment when things are ambiguous. For example:

- A pull request with only flaky-test failures might still be ready to merge
- A draft pull request from the user that hasn't been touched in weeks is stale even if CI is green

For "Needs Author Action" pull requests with CI failures, include the root cause diagnosis.

End with a recommended next action.
