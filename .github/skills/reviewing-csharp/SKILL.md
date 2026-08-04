---
name: reviewing-csharp
description: >-
  Reviews C# changes in the ImageBuilder codebase against the repo's modern C#
  conventions and invariants. Use when reviewing a C# diff, pull request, or
  staged/unstaged changes and you want anti-patterns caught before merge.
---

# Reviewing C# changes

Review changed C# code in dotnet/docker-tools for high-confidence violations of repository
conventions. Read `.github/instructions/csharp.instructions.md` first and treat it as the
source of truth for implementation guidance.

## Scope

- Review the C# diff and behavior directly affected by it. Do not report unrelated,
  pre-existing code.
- Report only issues with clear maintenance, correctness, or operational impact. Skip
  subjective style preferences and uncertain findings.
- Do not edit code.
- Use the exemplars in `.github/instructions/csharp.instructions.md` when recommending a
  repository-specific pattern.

## Review checks

### Language and API design

- New files use file-scoped namespaces and do not disable nullable reference types.
- New code is null-annotation clean. Flag `!` when it masks a nullable-correctness problem, but
  not deliberate negative tests such as passing `null!` to verify a guard.
- Use the least accessibility needed; do not introduce public APIs without a demonstrated
  caller.
- Reuse existing methods and services. Flag unused or speculative methods, parameters, and
  abstractions.

### Async

- Async methods end with `Async`; async calls are awaited rather than discarded.
- Thread a `CancellationToken` through async I/O.
- Do not add `async`/`await` when directly returning the task preserves behavior.
- Do not add `ConfigureAwait(...)`; this repository does not use it.

### Control flow and failures

- Use LINQ for transformations and loops for side effects or async work.
- Avoid multiple enumeration when it can change behavior or repeat meaningful work.
- Return empty collections rather than using `null` as control flow.
- Flag broad catches, success-shaped fallbacks, or swallowed failures without an explicit
  recovery path.

### Tests

- Use MSTest attributes, Shouldly for new assertions, and Moq for mocks.
- Tests must run in any order and in parallel; no shared mutable state or ordering assumptions.
- Reuse test helpers under `src/ImageBuilder.Tests/Helpers/`. Avoid disk I/O when an in-memory
  alternative or mock tests the same behavior.
- Do not add comments that only label Arrange, Act, or Assert sections.

### Documentation and comments

- New public or internal APIs have XML documentation where their contract is not obvious.
  Primary XML doc comments go directly on interfaces. Implementations use `<inheritdoc/>`.
- Comments should explain the intention behind code; do not accept comments that merely restate
  the code.
- Comments identify ambiguous input requirements such as fully qualified vs. relative
  paths, string formats, etc. Include examples where appropriate.

## Output format

Group findings by file. For each finding, return:

`path:line - <rule violated> - <concrete impact> - <suggested fix>`

End with `N issues to address` or `No convention violations found`.
