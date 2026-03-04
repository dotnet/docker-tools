# AGENTS.md

This file provides guidance to coding agents when working with code in this repository.

## Overview

This repository contains tooling for building and publishing Docker images in various .NET repos.
The primary tool is **ImageBuilder**, a .NET CLI application that orchestrates Docker image builds based on manifest metadata files.

## Project Structure

This repo contains several projects:

- **`src/ImageBuilder/`** - The main ImageBuilder CLI tool (executable)
- **`src/ImageBuilder.Models/`** - Shared model definitions for manifest files and image metadata
- **`src/ImageBuilder.Tests/`** - Unit tests using xUnit, Moq, and Shouldly
- **`eng/src/file-pusher/`** - Utility for pushing files to storage
- **`eng/src/yaml-updater/`** - Utility for updating YAML files

## Building and Testing

- To build all projects, run `dotnet build`.
- To run tests, run `dotnet test`.
- If you run into permission issues during builds relating to copying `.dll` files, run `dotnet clean` and/or `build.sh -clean` and then build again.

### Build ImageBuilder Container Image

See [src/README.md](src/README.md) for instructions on building single-platform and multi-arch ImageBuilder container images locally.

## ImageBuilder Architecture

ImageBuilder is a command-based CLI tool that operates on manifest files.
The manifest file (`manifest.json`) is the primary metadata source that defines which Docker images to build, their tags, platforms, and dependencies.

### Key Components

- **Commands** (`src/ImageBuilder/Commands/`) - Each command implements a specific operation (build, publish, generateBuildMatrix, etc.). Commands inherit from `Command<TOptions>` and use System.CommandLine for argument parsing.
- **Models** (`src/ImageBuilder.Models/Manifest/`) - Defines the manifest schema and image metadata structures
- **Services** - Abstracted services for Docker operations, Azure Container Registry interactions, Git operations, etc.

### Running ImageBuilder Locally

For quick validation during local development, use `dotnet run`:

```bash
dotnet run --project src/ImageBuilder -- --help
```

## `eng/docker-tools` Infrastructure

The `eng/docker-tools/` directory contains shared PowerShell scripts and Azure Pipelines templates used across all .NET Docker repositories.
This repository is the source of truth for these files - changes made here are automatically synchronized to consuming repositories.

For comprehensive documentation on the docker-tools infrastructure, pipeline architecture, image building workflows, and troubleshooting, see [eng/docker-tools/DEV-GUIDE.md](eng/docker-tools/DEV-GUIDE.md).

## ImageBuilder Build and Deployment Workflow

ImageBuilder is published as a container image to the Microsoft Artifact Registry (MAR) at `mcr.microsoft.com/dotnet-buildtools/image-builder`.
The ImageBuilder container image is defined in `src/manifest.json`.
It is built from Dockerfiles `src/Dockerfile.linux` and `src/Dockerfile.windows`.

ImageBuilder uses itself to build its own container image.
This means changes to ImageBuilder code and pipeline templates must be coordinated carefully in a two-step process.
The version of ImageBuilder used by pipelines is specified in `eng/docker-tools/templates/variables/docker-images.yml` (`imageNames.imageBuilder` variable).

### Two-Step Change Process

When making changes that affect both ImageBuilder code and pipeline behavior:

1. **Step 1: Update ImageBuilder code**
   - Make code changes to ImageBuilder source
   - Test locally using `dotnet run --project src/ImageBuilder`
   - Propose changes in a pull request and wait for it to be merged and for CI to run.
   - A pull request will automatically be created that updates `docker-images.yml` with the new ImageBuilder tag.
2. **Step 2: Update pipeline/manifest to use new ImageBuilder**
   - Once the new ImageBuilder image is published, pipeline templates in `eng/docker-tools/` can reference new features/commands
   - Consuming repositories will get the updated pipeline templates AND the new ImageBuilder image

### Why This Matters

- **Pipeline templates** in `eng/docker-tools/templates/` invoke ImageBuilder commands
- These templates run using a specific version/tag of the ImageBuilder container image
- If you add a new ImageBuilder command or change command behavior, pipelines can't use it until a new ImageBuilder image is published
- This means code changes and pipeline template changes that depend on them must be done in separate steps

### Bootstrap Option for Development

To test ImageBuilder code changes and pipeline template changes together without waiting for the two-step process, use the `bootstrapImageBuilder` parameter in the unofficial pipeline (`eng/pipelines/dotnet-buildtools-image-builder-unofficial.yml`).

When `bootstrapImageBuilder: true`:

- ImageBuilder is built from source at the start of every pipeline job
- The pipeline uses the freshly-built ImageBuilder instead of pulling from MCR
- This allows validating ImageBuilder changes and pipeline template changes in a single pipeline run

Once changes are validated together via the bootstrap process, then changes can be proposed via the normal two-step change process described above.

## Key File Formats

### manifest.json (Input)

Manifest files define metadata about which Docker images ImageBuilder will build. The schema is defined by the `Manifest` model in `src/ImageBuilder.Models/Manifest/Manifest.cs`. For detailed documentation, see [documentation/manifest-file.md](documentation/manifest-file.md).

### image-info.json (Output)

Image info files are ImageBuilder's output that describe which images were built.
The schema is defined by the `ImageArtifactDetails` model in `src/ImageBuilder.Models/Image/ImageArtifactDetails.cs`.
These files contain:

- Image digests for each built platform
- Tags applied to each image
- Dockerfile paths and commit information
- Build timestamps

Image info files are used by subsequent pipeline stages and are published to the versions repository to track what was built.
See [eng/docker-tools/DEV-GUIDE.md](eng/docker-tools/DEV-GUIDE.md) for more details on how image info flows through pipelines.

## Documentation Maintenance

When making changes to ImageBuilder, pipeline templates, or infrastructure:

- Update [eng/docker-tools/DEV-GUIDE.md](eng/docker-tools/DEV-GUIDE.md) if you change pipeline architecture, workflows, or add new capabilities
- Update [src/README.md](src/README.md) if you change how ImageBuilder container images are built
- Update [documentation/manifest-file.md](documentation/manifest-file.md) if you modify the manifest schema
- Update this file (AGENTS.md) if you add projects, change fundamental workflows, or modify architecture that affects how developers work with the codebase

Keep documentation synchronized with code changes so future developers have accurate guidance.
