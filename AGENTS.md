# Repository guidance

This repository contains tooling for building and publishing container images.
The primary tool is ImageBuilder, a .NET CLI app that orchestrates builds from manifest metadata.

## Project map

| Path | Purpose |
| --- | --- |
| `src/ImageBuilder/` | ImageBuilder CLI and commands |
| `src/ImageBuilder.Models/` | Manifest and image metadata models |
| `src/ImageBuilder.Tests/` | MSTest, Moq, and Shouldly tests |
| `eng/src/file-pusher/` | Storage file-pushing utility |
| `eng/src/yaml-updater/` | YAML update utility |
| `eng/docker-tools/` | Shared scripts and Azure Pipelines templates synchronized to other .NET Docker repositories |

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

- `eng/docker-tools/` is the source of truth for infrastructure synchronized to consuming
  repositories. Document breaking changes there in `eng/docker-tools/CHANGELOG.md` with
  actionable migration steps.
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

## ImageBuilder deployment constraint

ImageBuilder builds its own published container image. Pipelines select that image through
`imageNames.imageBuilder` in `eng/docker-tools/templates/variables/docker-images.yml`.
Changes that require both new ImageBuilder behavior and pipeline adoption normally use two pull requests:

1. Merge and publish the ImageBuilder code change.
2. After automation updates the ImageBuilder tag, merge the dependent pipeline or manifest change.

For combined development validation, use the engineering validation unofficial pipeline defined
in `eng/pipelines/dotnet-docker-tools-eng-validation-unofficial.yml` with the parameter
`bootstrapImageBuilder: true`; each job then builds and uses ImageBuilder from the current source.

## Bundled infrastructure

ImageBuilder embeds `eng/docker-tools/` under `src/Infrastructure/Content/` so source and
pipeline-template changes can be developed together. The copy lives under `src/` because that is
the ImageBuilder container build context.

The copies intentionally differ between releases: update `src/Infrastructure/Content/` for content
the next ImageBuilder will ship; automation refreshes `eng/docker-tools/` when the repository
adopts that ImageBuilder version. See `src/Infrastructure/README.md`.

## Documentation

Update only the narrowest documentation affected by the change:

| Change | Documentation |
| --- | --- |
| Pipeline architecture, workflows, or capabilities | `eng/docker-tools/DEV-GUIDE.md` |
| ImageBuilder container-image build workflow | `src/README.md` |
| Manifest schema | `documentation/manifest-file.md` |
| Breaking shared template behavior | `eng/docker-tools/CHANGELOG.md` |
| Fundamental project or agent workflow | `AGENTS.md` |
