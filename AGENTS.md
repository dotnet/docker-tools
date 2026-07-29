# Repository guidance

This repository contains tooling for building and publishing container images.
The primary tool is ImageBuilder, a .NET CLI app that orchestrates builds from manifest metadata.

## Project map

| Path | Purpose |
| --- | --- |
| `src/ImageBuilder/` | ImageBuilder CLI and commands |
| `src/ImageBuilder.Models/` | Manifest and image metadata models |
| `src/ImageBuilder.Tests/` | MSTest, Moq, and Shouldly tests |
| `src/ImageBuilder.Updater/` | ImageBuilder infrastructure update PR utility |
| `src/Infrastructure/Content/` | Source for shared infrastructure shipped in the next ImageBuilder |
| `eng/docker-tools/` | Shared infrastructure consumed by this repository |

ImageBuilder commands inherit from `Command<TOptions>` and use System.CommandLine.
The manifest schema starts at `src/ImageBuilder.Models/Manifest/Manifest.cs`; generated image
metadata starts at `src/ImageBuilder/Models/Image/ImageArtifactDetails.cs`.

## Build and validation

- Build, test, and pack on Windows: `build.cmd`
- Build, test, and pack on Linux or macOS: `./build.sh`
- Run ImageBuilder locally after building:
  `.dotnet/dotnet run --project src/ImageBuilder -- --help` (use `.dotnet/dotnet.exe` on Windows).

See `src/README.md` for local ImageBuilder container-image build instructions.

## Repository invariants

- Make shared infrastructure changes in `src/Infrastructure/Content/`; they automatically flow to
  `eng/docker-tools/` through the ImageBuilder update process.
- Files under `eng/common/` come from dotnet/arcade and are overwritten by automation. Do not
  edit them here; make the source change in Arcade.
- `publishConfig` is the source of truth for registry authentication. Registry service
  connections belong in `publishConfig.RegistryAuthentication`; non-registry connections
  remain separate or use `additionalServiceConnections`.
- ImageBuilder runs in a container. Pass `SYSTEM_ACCESSTOKEN` and `SYSTEM_OIDCREQUESTURI`
  explicitly into that container for OIDC authentication.
- Jobs using `AzurePipelinesCredential` must include
  `reference-service-connections.yml` with only the connections they need. Templates
  supporting Linux and Windows must pass `dockerClientOS`.

## Documentation

Update only the narrowest documentation affected by the change:

| Change | Documentation |
| --- | --- |
| Pipeline architecture, workflows, or capabilities | `eng/docker-tools/DEV-GUIDE.md` |
| ImageBuilder container-image build workflow | `src/README.md` |
| Manifest schema | `documentation/manifest-file.md` |
| Breaking change to shared infrastructure | `src/Infrastructure/Content/CHANGELOG.md` |
| Fundamental project or agent workflow | `AGENTS.md` |
