# Docker Tools

This is a repo to house some common tools for use in the various .NET Docker repos.

## Image Builder

A tool used to build and publish Docker images.

The Image Builder tool can be acquired via a Docker image available at [mcr.microsoft.com/dotnet-buildtools/image-builder](https://mcr.microsoft.com/v2/dotnet-buildtools/image-builder/tags/list) or built from source via the instructions in its [readme](./src/README.md).

The Image Builder tool relies on metadata which defines various information needed to build and tag Docker images.  The metadata is stored in a manifest.json file ([sample](https://github.com/dotnet/dotnet-docker/blob/main/manifest.json)).  The metadata schema is defined in [source](./src/ImageBuilder/Models/Manifest/Manifest.cs).

The full list of supported commands can be seen by running the tool.

```console
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock mcr.microsoft.com/dotnet-buildtools/image-builder --help
```

The list of supported options for each command can be seen by specifying the `--help` option:

```console
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock mcr.microsoft.com/dotnet-buildtools/image-builder build --help
```
