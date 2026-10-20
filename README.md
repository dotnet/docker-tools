# Docker Tools

This is a repo to house some common tools for use in the various .NET Docker repos.

## Tools

- [ImageBuilder](./src/README.md) is a tool used to build and publish Docker images.

## Building locally

To build, test, and pack all projects in the repo, run one of the following scripts:

- **Windows**: `build.cmd`
- **Linux/Mac**: `./build.sh`

## Code formatting

C# code style and formatting rules are configured in [`.editorconfig`](./.editorconfig) and enforced during the build and in CI.
After running the build script to initialize the local .NET SDK, use `dotnet format` to format code or verify formatting:

- **Format code**: `.dotnet/dotnet format`
- **Verify formatting**: `.dotnet/dotnet format --verify-no-changes`

## Feature branches

ImageBuilder images are published from the `main` branch as well as `feature/*` branches.
Run [`pwsh eng/Get-FeatureBranches.ps1`](./eng/Get-FeatureBranches.ps1) to see all available feature branches
