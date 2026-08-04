# Docker Tools

This is a repo to house some common tools for use in the various .NET Docker repos.

## Tools

- [ImageBuilder](./src/README.md) is a tool used to build and publish Docker images.

## Building locally

To build, test, and pack all projects in the repo, run one of the following scripts:

- **Windows**: `build.cmd`
- **Linux/Mac**: `./build.sh`

## Feature branches

ImageBuilder images are published from the `main` branch as well as `feature/*` branches.
Run [`pwsh eng/Get-FeatureBranches.ps1`](./eng/Get-FeatureBranches.ps1) to see all available feature branches
