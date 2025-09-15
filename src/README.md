# ImageBuilder

ImageBuilder is a tool used to build and publish Docker images.

## Building the ImageBuilder container image

All commands are relative to the root of the repo.

### Build a single-platform image

Using Linux or Windows, simply run the build script:

```pwsh
# From src/Microsoft.DotNet.ImageBuilder
pwsh -f build.ps1

# From the root of the repo
pwsh -wd ./src/Microsoft.DotNet.ImageBuilder/ -f src/Microsoft.DotNet.ImageBuilder/build.ps1
```

### Build a multi-arch Linux image

If you don't need to test on Windows, this is the easiest way to create a multi-arch manifest list.

```pwsh
# Build the image. Choose one or both platforms, and optionally push to a registry or load the image locally.
docker buildx build [--push,--load] --platform [linux/amd64,linux/arm64] -t "${REPO}:${TAG}" -f .\src\Microsoft.DotNet.ImageBuilder\Dockerfile.linux .\src\Microsoft.DotNet.ImageBuilder\
```

### Create a multi-platform manifest list

First, build and push Linux and Windows images separately.
Gather the specific digests for the images you want to put into one manifest list.
Then, create the manifest list and push it:

```pwsh
docker manifest create "${REPO}:${TAG}" "${REPO}@sha256:abcde12345" "${REPO}@sha256:fghij67890"
docker manifest push "${REPO}:${TAG}"
```
