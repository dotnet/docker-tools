# ImageBuilder

ImageBuilder is a tool used to build and publish Docker images. ImageBuilder itself is packaged as a container image:

- `docker pull mcr.microsoft.com/dotnet-buildtools/image-builder:latest`
- [List of all ImageBuilder image tags](https://mcr.microsoft.com/v2/dotnet-buildtools/image-builder/tags/list)

ImageBuilder relies on metadata which defines various information needed to build and tag container images. The metadata is stored in a manifest.json file ([sample](https://github.com/dotnet/dotnet-docker/blob/main/manifest.json)). The metadata schema is defined in [source](./src/ImageBuilder/Models/Manifest/Manifest.cs).

The full list of supported commands can be seen by running the tool.

```console
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock mcr.microsoft.com/dotnet-buildtools/image-builder --help
```

Specific options for each command can be seen by specifying the `--help` option:

```console
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock mcr.microsoft.com/dotnet-buildtools/image-builder build --help
```

## Building the ImageBuilder container image

All commands are relative to the root of the repo.

### Build a single-platform image

Using Linux or Windows, simply run the build script:

```pwsh
# From src/
pwsh -f build.ps1

# From the root of the repo
pwsh -wd ./src -f src/build.ps1
```

### Build a multi-arch Linux image

If you don't need to test on Windows, this is the easiest way to create a multi-arch manifest list.

```pwsh
# Build the image. Choose one or both platforms, and optionally push to a registry or load the image locally.
docker buildx build [--push,--load] --platform [linux/amd64,linux/arm64] -t "${REPO}:${TAG}" -f ./src/Dockerfile.linux ./src/
```

### Create a multi-platform manifest list

First, build and push Linux and Windows images separately.
Gather the specific digests for the images you want to put into one manifest list.
Then, create the manifest list and push it:

```pwsh
docker manifest create "${REPO}:${TAG}" "${REPO}@sha256:abcde12345" "${REPO}@sha256:fghij67890"
docker manifest push "${REPO}:${TAG}"
```
