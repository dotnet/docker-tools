---
applyTo: "**/*.cs"
---

# C# guidance for ImageBuilder

## Style anchors

The codebase mixes old and modern styles. For new or modified code, use these files as style
anchors rather than copying patterns from nearby legacy code.

Type of code | Good examples
------------ | -------------
Command | `src/ImageBuilder/Commands/Signing/SignImagesCommand.cs`, `src/ImageBuilder/Commands/Signing/SignImagesOptions.cs`
Service | `src/ImageBuilder/Oras/IOrasService.cs`, `src/ImageBuilder/Oras/OrasDotNetService.cs`
Configuration | `src/ImageBuilder/Configuration/RegistryAuthentication.cs`, `src/ImageBuilder/Configuration/PublishConfiguration.cs`
Data type | `src/ImageBuilder/Signing/ImageSigningRequest.cs`

Prefer simple, direct code. Add helper layers, pass-through methods, factories, or interfaces
only when they remove real complexity.

## Commands and inputs

Expose ImageBuilder functionality through commands. Command classes have one
`ExecuteAsync()` method.

- Use types in `Microsoft.DotNet.ImageBuilder.Configuration`, populated by
  `appsettings.json`, for strongly typed values that stay constant across invocations of the
  same pipeline.
- Define CLI arguments in options classes for values that can vary by invocation, such as
  paths, switches, prefixes, filters, and timestamps.
- Some legacy commands use CLI arguments for configuration values. Migrate them only when
  that work is in scope.

## Services

Commands own orchestration and make the control flow visible. Services encapsulate bounded
implementation details behind narrow, domain-based APIs; they should not hide the command's
workflow. Give services predictable behavior, observability, runtime characteristics, and
failure modes. Use idiomatic .NET patterns.

## Logging

Follow best practices for Microsoft.Extensions.Logging: use message templates with named
properties instead of interpolation when logging data. Keep info-level logs low-noise and
put infrastructure details such as HTTP request logs at debug (or lower) level.

## Data types

Define new data types used by commands and services as immutable snapshots. Choose an
appropriate immutable representation, such as a record, readonly record struct, or class with
get-only properties. Prefer LINQ expressions when they keep transformations clear.

Manifest schema models under `src/ImageBuilder.Models` are an exception: they must remain
mutable classes for JSON and schema compatibility.
