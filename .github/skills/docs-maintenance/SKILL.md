---
name: docs-maintenance
description: >-
  Updates or reviews repository documentation after changes to user behavior,
  shared contracts, pipelines, workflows, or agent guidance.
---

# Documentation maintenance

Keep dotnet/docker-tools documentation accurate and scoped to the audience affected by a
change.

## Mode

- **Update:** edit the required documentation.
- **Review:** report missing, unnecessary, or misleading documentation changes without
  editing.

Infer the mode from the request. Requests to update, fix, add, or write documentation authorize
updates; requests to check, review, or verify documentation use review mode.

## Workflow

1. Inspect the diff and identify changes to behavior, contracts, workflows, generated outputs,
   and agent expectations.
2. Decide whether contributors, consumers, users, or agents need new information. Documentation
   is normally unnecessary for internal refactors, mechanical changes, test-only changes, or
   fixes whose intended behavior is already documented.
3. Choose the narrowest document for the affected audience. Do not duplicate guidance unless
   distinct audiences need it.
4. In update mode, make the documentation changes. Prefer exact commands, paths, examples, and
   migration steps. Preserve the existing structure unless it is misleading.
5. In review mode, report only actionable documentation gaps or unnecessary changes.

Authentication documentation must distinguish Azure service connections, API authentication,
git credentials, and pipeline variables when more than one appears in the change.

Do not edit files under `eng/common/`; they come from dotnet/arcade and will be overwritten.

## Documentation map

| Document | Update when |
| --- | --- |
| `README.md` | The repository purpose, tool list, or standard build and test entrypoints change. |
| `AGENTS.md` | Repo-wide project structure, commands, source-of-truth rules, workflows, or agent invariants change. |
| `.github/instructions/*.md` | High-level design guidance that shapes how an agent architects a change needs to be added, updated, or removed. Keep these files focused: they guide structural decisions and should not contain mechanical checklists. |
| `.github/agents/*.md`, `.github/skills/*/SKILL.md` | An opt-in agent or reusable operational workflow changes. Review skills (such as `reviewing-csharp`) contain mechanical checklists of coding standards enforced during review; update them when a specific standard needs to be added, tightened, or removed. |
| `src/README.md` | ImageBuilder invocation, command discovery, packaging, or local container-image builds change. |
| `documentation/manifest-file.md` | Manifest concepts, schema generation, or authoring guidance change. |
| `documentation/signing.md` | Signing configuration, credentials, certificates, trust stores, or pipeline flow change. |
| `documentation/base-image-dependency-flow.md` | Base-image subscriptions, digest tracking, image-info usage, or rebuild triggers change. |
| `eng/docker-tools/DEV-GUIDE.md` | Shared pipeline architecture, scripts, ImageBuilder integration, authentication, publishing, testing, or troubleshooting change. |
| `eng/docker-tools/CHANGELOG.md` | A shared change affects downstream repositories, changes a contract or default, adds a capability, or requires migration. |
| `eng/tests/pipeline-validation/**/README*.md` | A pipeline-validation fixture intentionally requires different README content. |

For `eng/docker-tools/CHANGELOG.md`, include the date, issue or pull request when known, what
changed, and required downstream action. Do not add entries for internal changes with no
consumer impact.

## Output

- **Update:** list each document changed and why; mention intentionally unchanged documents only
  when the choice is non-obvious.
- **Review:** report each gap as `changed area - required document - reason`. Report unnecessary
  updates only when they would be stale, duplicated, or misleading. If none are needed, say so.
