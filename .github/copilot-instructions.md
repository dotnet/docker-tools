# GitHub Copilot Instructions for dotnet/docker-tools

This repository contains common tools for .NET Docker repositories, with the primary tool being **ImageBuilder** - a specialized .NET application for building, testing, and publishing Docker images across multiple platforms and architectures.

## Repository Structure

- **`src/`** - Contains the ImageBuilder tool and its components
  - `ImageBuilder/` - Main tool source code (C# .NET)
  - `ImageBuilder.Models/` - Data models and manifest schema
  - `ImageBuilder.Tests/` - Unit tests
  - `Dockerfile.linux` and `Dockerfile.windows` - ImageBuilder container images
- **`eng/docker-tools/`** - Shared infrastructure synchronized across all .NET Docker repositories
  - **`DEV-GUIDE.md`** - **CRITICAL**: Comprehensive developer guide for using the docker-tools infrastructure (see Developer Guide section below)
  - PowerShell scripts for local development (`build.ps1`, `Invoke-ImageBuilder.ps1`)
  - Azure Pipelines templates for CI/CD
- **`documentation/`** - User-facing documentation
  - `manifest-file.md` - Manifest schema documentation
  - `base-image-dependency-flow.md` - Base image update automation

## Developer Guide Maintenance

**IMPORTANT**: The `eng/docker-tools/DEV-GUIDE.md` file is the authoritative guide for developers using this infrastructure. When making changes to the docker-tools infrastructure:

1. **Always update DEV-GUIDE.md** when you modify:
   - PowerShell scripts in `eng/docker-tools/`
   - Azure Pipeline templates
   - ImageBuilder commands or parameters
   - Build/test/publish workflows
   - Any infrastructure behavior or conventions

2. **Keep examples current**: The DEV-GUIDE.md contains practical examples and workflows - ensure they remain accurate and functional.

3. **Document breaking changes**: If changes affect consuming repositories (dotnet-docker, dotnet-buildtools-prereqs-docker, dotnet-framework-docker), clearly document migration steps.

## Code Review Guidelines

When making changes to this repository, always consider which documentation needs to be updated alongside your code changes. Common patterns:

### Changes to Shared Pipeline Templates

When modifying files in `eng/docker-tools/` (PowerShell scripts, Azure Pipeline templates, workflow changes):
- **Update `eng/docker-tools/DEV-GUIDE.md`** with any new commands, changed parameters, or updated workflows
- Ensure examples in DEV-GUIDE.md remain accurate and functional
- Document any breaking changes that affect consuming repositories

### Changes to Repository Build Scripts

When modifying `build.sh`, `build.cmd`, or the build process:
- **Update `README.md` section "Building locally"** to reflect new build commands or options
- Keep instructions accurate for both Windows and Linux/Mac users
- Document any new prerequisites or environment requirements

### Changes to ImageBuilder

When modifying ImageBuilder source code (`src/ImageBuilder/`), build scripts, or processes:
- **Update `src/README.md`** to reflect changes in:
  - How to build the ImageBuilder container image
  - New commands or command options
  - Changed build processes or requirements
- Update `documentation/manifest-file.md` if manifest schema changes
- Ensure code examples and usage patterns remain current

### General Documentation Updates

Always ask yourself:
- Does this change affect how developers use the tool?
- Does this change affect the build or test process?
- Does this change require updates to examples or workflows in documentation?
- Are there breaking changes that need to be communicated?

If the answer to any of these is "yes", update the relevant documentation as part of your PR.

## Building and Testing

### Build the entire repository
```bash
# Linux/Mac
./build.sh

# Windows
build.cmd
```

This runs: restore → build → test → pack

### Build ImageBuilder container image
```powershell
# From src/ directory
./build.ps1

# Or from root directory
pwsh -f ./src/build.ps1
```

### Run tests
```bash
# From root - runs restore, build, test, and pack
./build.sh --test
```

```powershell
# From src/ - runs ImageBuilder tests specifically
./run-tests.ps1
```

## ImageBuilder Architecture

ImageBuilder is a CLI tool that orchestrates Docker image builds using manifest files (`manifest.json`). Key concepts:

- **Manifest schema**: Defined in `src/ImageBuilder/Models/Manifest/Manifest.cs`
- **Image info files**: Track build metadata (`ImageArtifactDetails` class)
- **Multi-platform support**: Handles Linux (Alpine, Ubuntu, Azure Linux) and Windows (Server Core, Nano Server)
- **Multi-architecture**: Supports amd64, arm64, arm32
- **Dependency graphs**: Builds images in correct order based on base image dependencies

## Infrastructure Synchronization

**Critical**: Files in `eng/docker-tools/` are synchronized across repositories by automation. Changes made directly in consuming repositories will be overwritten.

- **To modify infrastructure**: Submit changes to this repository (dotnet/docker-tools)
- **Consuming repos**: dotnet-docker, dotnet-buildtools-prereqs-docker, dotnet-framework-docker
- **Sync frequency**: Updates are pushed automatically when merged to main

## Common Workflows

### Local Development
```powershell
# Build specific images locally
./eng/docker-tools/build.ps1 -OS "alpine" -Architecture "arm64"

# Run ImageBuilder directly
./eng/docker-tools/Invoke-ImageBuilder.ps1 "build --help"
```

### CI/CD Pipeline
- **Public PRs**: Build and test in dry-run mode (no registry pushes)
- **Official builds**: Build → Test → Publish to Microsoft Artifact Registry (MAR)
- **Matrix generation**: `generateBuildMatrix` command creates parallel build jobs
- **Stages control**: Use `stages` variable to run specific pipeline stages

## Key Files to Keep in Sync

When updating functionality, these files often need coordinated changes:

1. **ImageBuilder source code** (`src/ImageBuilder/`)
2. **Pipeline templates** (`eng/docker-tools/templates/`)
3. **PowerShell scripts** (`eng/docker-tools/*.ps1`)
4. **DEV-GUIDE.md** - Document changes here!
5. **Manifest schema docs** (`documentation/manifest-file.md`)

## Additional Context

- **.NET SDK Version**: Defined in `global.json`
- **Arcade SDK**: Uses Microsoft.DotNet.Arcade.Sdk for build infrastructure
- **Testing**: xUnit for unit tests
- **Container registry**: Uses Azure Container Registry (ACR) for staging
- **Final publishing**: Microsoft Artifact Registry (MAR) and Docker Hub

## Remember

- Keep DEV-GUIDE.md updated with infrastructure changes
- Maintain backward compatibility when possible for consuming repositories
- Test changes locally before submitting PRs
- Follow existing code patterns and conventions
- All warnings are treated as errors - fix them
