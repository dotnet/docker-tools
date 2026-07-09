---
name: docs-maintenance
description: >-
  Keeps repository documentation current. Use after code, pipeline, manifest,
  infrastructure, or agent-instruction changes to either update docs or check
  for missing documentation updates.
---

# Documentation maintenance

Use this skill to keep dotnet/docker-tools documentation accurate, discoverable, and
appropriately scoped as the repo changes.

This skill can run in two modes:

- **Update mode:** analyze the current changes and edit the relevant docs.
- **Review mode:** analyze the current changes and report missing or unnecessary doc
  updates without editing unless the user asks you to.

If the user does not specify a mode, infer it from the request. If they ask to "update",
"fix", "add", or "write" docs, use update mode. If they ask to "check", "review",
"verify", or "look for missing docs", use review mode.

## Workflow

1. Start from the diff.
   - Use `git diff --stat`, `git diff --name-only`, and targeted `git diff -- <path>`.
   - Identify changed behavior, contracts, workflows, generated outputs, and agent
     expectations before reading docs.
2. Decide whether docs are required.
   - Use the update triggers below.
   - If the change is not user-visible and does not affect how contributors, consumers,
     or agents should work, say that no doc update is required.
3. Choose the narrowest matching doc.
   - Do not duplicate the same guidance across multiple docs unless each audience needs it.
   - Prefer exact commands, file paths, examples, and migration steps over broad explanation.
4. In update mode, edit the docs.
   - Keep docs concise and practical.
   - Lead with what changed or what problem is fixed.
   - Explain why a change exists when it is not obvious.
   - Do not add irrelevant sentences just because they are true.
   - Avoid em dashes and em-dash-like rewrites.
   - Preserve existing structure unless it is misleading.
   - Do not edit files under `eng/common/` unless the user explicitly says they are making
     the corresponding Arcade change.
5. In review mode, report the missing or unnecessary doc updates.

## Documentation map

| Doc | Purpose | Update when |
| --- | --- | --- |
| `README.md` | Repo landing page: what this repo contains and how to build/test it. | The repo's top-level purpose, tool list, or standard build/test entrypoints change. |
| `AGENTS.md` | Repo-wide instructions for coding agents. | Project structure, build/test commands, source-of-truth rules, required workflows, or agent-relevant invariants change. Keep it short. |
| `.github/instructions/*.md` | Lightweight implementation guidance that loads automatically for matching files. | Implementing agents need different exemplars or context to write code correctly. Prefer positive examples over review checklists. |
| `.github/agents/*.md` | Specialized custom agents for review, maintenance, investigation, or other opt-in workflows. | A specialized agent's scope, trigger conditions, review rules, or expected output changes. |
| `.github/skills/*/SKILL.md` | Reusable operational workflows for the CLI. | A repeated diagnostic, triage, documentation, or maintenance workflow changes. |
| `src/README.md` | User-facing ImageBuilder usage and local ImageBuilder container-image build instructions. | ImageBuilder invocation, supported command discovery, packaging, or container-image build workflow changes. |
| `documentation/manifest-file.md` | Manifest file overview and schema discovery. | Manifest schema concepts, schema generation, or manifest authoring guidance changes. |
| `documentation/signing.md` | Container image signing setup and operation. | Signing configuration, required variables, certificates, trust-store behavior, or pipeline signing flow changes. |
| `documentation/base-image-dependency-flow.md` | Conceptual flow for base-image update detection and rebuild automation. | Base-image subscription, digest tracking, image-info usage, or rebuild-trigger behavior changes. |
| `eng/docker-tools/readme.md` | Warning and entrypoint for synchronized shared docker-tools infrastructure. | Rarely. Only update if the synchronization/source-of-truth guidance changes. |
| `eng/docker-tools/DEV-GUIDE.md` | Practical guide for using and understanding shared docker-tools infrastructure. | Pipeline architecture, local scripts, ImageBuilder integration, service connections, publishing, testing, troubleshooting, or shared infrastructure capabilities change. |
| `eng/docker-tools/CHANGELOG.md` | Chronological notes for `eng/docker-tools` breaking changes and notable new features. | A change affects downstream repos, changes template parameters/contracts, requires migration, or adds notable shared infrastructure behavior. Include actionable migration steps for breaking changes. |
| `eng/tests/pipeline-validation/**/README*.md` | Test fixture content for pipeline validation. | Only when tests intentionally require different fixture README content. |
| `eng/common/**` docs | Arcade-managed documentation. | Do not edit here from this repo unless explicitly coordinating an Arcade-source change. |

## When docs are required

Require documentation updates for changes that affect any of these:

- **User-visible ImageBuilder behavior:** commands, options, outputs, image packaging,
  local execution, or supported workflows.
- **Public or shared file formats:** manifest JSON, image-info JSON, publish
  configuration, subscription files, or generated artifacts consumed by other repos.
- **Shared pipeline infrastructure:** templates, parameters, variables, service
  connections, authentication requirements, publishing flow, signing flow, testing flow,
  or synchronization behavior under `eng/docker-tools/`.
- **Breaking changes for downstream repos:** removed or renamed templates/parameters,
  changed defaults, changed required variables, new authentication requirements, changed
  artifact locations, or behavior that requires a consuming repo migration.
- **Operational workflows:** recurring triage, investigation, release, or maintenance
  procedures that contributors or agents are expected to follow.
- **Agent behavior:** coding instructions, custom agent scope, review invariants, skills,
  or source-of-truth guidance for agents.

Authentication and token handling must be especially explicit. Separate Azure service
connection behavior, API authentication, git CLI tokens, and pipeline variables when those
concepts appear in the same change.

Docs are usually not required for:

- Internal refactors with no behavior, contract, workflow, or contributor impact.
- Test-only changes that do not change expected fixture content or documented behavior.
- Bug fixes whose correct behavior was already documented accurately.
- Mechanical formatting, dependency updates, or build cleanup with no user-facing impact.

## Changelog expectations

For changes under `eng/docker-tools/`, decide separately whether the changelog is required:

- Add an entry to `eng/docker-tools/CHANGELOG.md` for breaking changes, downstream
  migrations, new template parameters, new shared capabilities, changed defaults, or
  behavior that consumers need to know about.
- The entry should include the date, issue or pull request if known, what changed, and
  what downstream repos must do.
- Do not add changelog entries for purely internal refactors or fixes that do not affect
  downstream repos.

## Output

In update mode:

1. List the docs you changed and why.
2. State any docs you intentionally did not change when that decision is non-obvious.

In review mode:

1. Report missing doc updates as `path or area - required doc - reason`.
2. Report unnecessary doc updates only when they create stale, duplicate, or misleading
   guidance.
3. If no doc changes are needed, say so plainly.
