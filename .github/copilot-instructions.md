# GitHub Copilot Instructions for dotnet/docker-tools

Common tools for .NET Docker repositories, primarily **ImageBuilder** - a CLI tool for building, testing, and publishing Docker images across multiple platforms and architectures.

## Key Documentation

- **[README.md](../README.md)** - Building the repo locally
- **[src/README.md](../src/README.md)** - Building ImageBuilder container image, available commands
- **[eng/docker-tools/DEV-GUIDE.md](../eng/docker-tools/DEV-GUIDE.md)** - Local development workflows, pipeline architecture, CI/CD patterns
- **[documentation/manifest-file.md](../documentation/manifest-file.md)** - Manifest schema documentation

## Documentation Maintenance

Update documentation alongside code changes:

| When you modify...                                | Update...                         |
| ------------------------------------------------- | --------------------------------- |
| `eng/docker-tools/` (scripts, pipeline templates) | `eng/docker-tools/DEV-GUIDE.md`   |
| `build.sh`, `build.cmd`                           | `README.md`                       |
| ImageBuilder source (`src/ImageBuilder/`)         | `src/README.md`                   |
| Manifest schema                                   | `documentation/manifest-file.md`  |

## Infrastructure Synchronization

Files in `eng/docker-tools/` are synchronized across repositories by automation. Changes made directly in consuming repositories will be overwritten. Submit infrastructure changes to this repository.

When making changes to `eng/docker-tools`, take special care to document breaking changes that affect consuming repositories.

## Code Conventions

For all new C# code in this repository:

- Enable nullable reference annotations, even if only for part of the file.
- Use file-scoped namespaces.
- Follow existing code patterns.
