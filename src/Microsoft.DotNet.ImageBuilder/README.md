# ImageBuilder

ImageBuilder is a tool used to build and publish Docker images.

## Building the ImageBuilder container image

All commands are relative to the root of the repo.

### Linux

#### Build a single-platform Linux image

```pwsh
# Build the image
docker build -t "${REPO}:${TAG}-linux-amd64" -f .\src\Microsoft.DotNet.ImageBuilder\Dockerfile.linux .\src\Microsoft.DotNet.ImageBuilder\

# Push the tag
docker push "${REPO}:${TAG}-linux-amd64"
```

#### Build a multi-arch Linux image

If you don't need to test on Windows, this is the easiest way to create a multi-arch manifest list.

```pwsh
# Build the image. Choose one or both platforms, and optionally push to a registry or load the image locally.
docker buildx build [--push,--load] --platform [linux/amd64,linux/arm64] -t "${REPO}:${TAG}" -f .\src\Microsoft.DotNet.ImageBuilder\Dockerfile.linux .\src\Microsoft.DotNet.ImageBuilder\
```

### Windows

```pwsh
# Choose one of each
$WINDOWS_BASE=["servercore:ltsc2016-amd64","nanoserver:1809-amd64","nanoserver:ltsc2022-amd64"]
$WINDOWS_SDK=["nanoserver-1809","nanoserver-ltsc2022"]

docker build --build-arg WINDOWS_BASE="${WINDOWS_BASE}" --build-arg WINDOWS_SDK="${WINDOWS_SDK}" -t "${REPO}:${TAG}-windows-amd64" -f .\src\Microsoft.DotNet.ImageBuilder\Dockerfile.windows .\src\Microsoft.DotNet.ImageBuilder\

# Push the tag
docker push "${REPO}:${TAG}-windows-amd64"
```

### Create a multi-platform manifest list

First, build Linux and Windows images separately.
Gather the specific digests for the images you want to put into one manifest list.
Then, create the manifest list and push it:

```pwsh
docker manifest create "${REPO}:${TAG}" "${REPO}@sha256:abcde12345" "${REPO}@sha256:fghij67890"
docker manifest push "${REPO}:${TAG}"
```
