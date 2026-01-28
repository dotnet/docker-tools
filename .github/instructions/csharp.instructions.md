---
applyTo: "**/*.cs"
---

# C#/.NET Guidelines

## Coding style

For all new C# code:

- Use file-scoped namespaces.
- Use nullable reference types
    - Use `<Nullable>enable</Nullable>` in project files for new projects.
    - When adding code to existing files without nullable reference types enabled, add `#nullable enable` followed by `#nullable restore` in source files.
- Use collection expresions - write `[1, 2, 3]` and not `new List<int> { 1, 2, 3 }`.
- Use `var` for local variable declarations.
- Use switch expressions and pattern matching.
- Use string interpolation (`$"Hello, {name}!"`) instead of `string.Format` or concatenation.
- Use `"""triple-quoted strings"""` for multi-line string literals. These can be interpolated as well.
- Use expression-bodied members for simple getters and setters.

## Code Design Rules

- Use immutable records instead of classes for DTOs.
- Do not default to `public` accessibility for members and classes. Follow the least-exposure rule: `private` > `internal` > `protected` > `public`
- Do not add unused methods/parameters for use cases that were not asked for.
- Reuse existing methods or services as much as possible.
- Use composition over inheritance.

## Error Handling & Edge Cases

- Guard early; use `string.IsNullOrWhiteSpace`, `ArgumentNullException.ThrowIfNull`, or `ArgumentNullException.ThrowIfNullOrWhiteSpace`.
- Avoid using null for control flow.
- In methods that return collections, return empty collections instead of null.
- The null-forgiving operator (`!`) is **always** a code smell.
- Do not add excessive or unnecessary try/catch blocks within the same assembly.

## Async Best Practices

- All async methods must have names ending with `Async`.
- Always await async methods - do not "fire and forget".
- Always accept and pass along a `CancellationToken` in async code.
- Don’t add `async/await` if you can simply return a Task directly.

## Testing

- Use Shouldly when writing new assertions.
- Use clear assertions that verify the outcome expressed by the test name.
- Tests should be able to run in any order or in parallel.
- Use or add helper methods for constructing mocks and complex test data objects.
- Avoid disk I/O if possible; use in-memory alternatives or mocks.
- Do not add "Arrange-Act-Assert" comments.

## Comments

- Add XML documentation comments for for new **public** or **internal** types and members.
- Comments that simply restate the member or parameter name do not provide value.
- Comments should provide additional context or explain non-obvious behavior, especially for parameters.
- Comments inside methods should explain "why," not "what".
- Avoid redundant comments that restate the code - not all code needs comments.
