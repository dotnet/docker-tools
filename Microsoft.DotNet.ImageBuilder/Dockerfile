# Use this Dockerfile to create a runner image
#     docker build -t image-builder .
#     docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v <local path to build>:/repo -w /repo image-builder <image-build args>

# build Microsoft.DotNet.ImageBuilder
FROM microsoft/dotnet:2.0-sdk AS build-env
WORKDIR image-builder

# restore packages before copying entire source - provides optimizations when rebuilding
COPY Microsoft.DotNet.ImageBuilder.sln ./
COPY NuGet.config ./
COPY src/Microsoft.DotNet.ImageBuilder.csproj ./src/
COPY tests/Microsoft.DotNet.ImageBuilder.Tests.csproj ./tests/
RUN dotnet restore Microsoft.DotNet.ImageBuilder.sln

# copy everything else and build
COPY . ./
RUN dotnet build Microsoft.DotNet.ImageBuilder.sln
RUN dotnet test tests/Microsoft.DotNet.ImageBuilder.Tests.csproj
RUN dotnet publish ./src/Microsoft.DotNet.ImageBuilder.csproj -c Release -o out


# build runtime image
FROM microsoft/dotnet:2.0-runtime

# install Docker
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        apt-transport-https \
        gnupg \
        software-properties-common \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://download.docker.com/linux/debian/gpg | apt-key add - \
    && add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/debian $(lsb_release -cs) stable" \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        docker-ce \
    && rm -rf /var/lib/apt/lists/*

# install manifest-tool
RUN curl -fsSL "https://github.com/estesp/manifest-tool/releases/download/v0.6.0/manifest-tool-linux-amd64" \
        -o /usr/local/bin/manifest-tool \
    && chmod +x /usr/local/bin/manifest-tool

# install image-builder
WORKDIR image-builder
COPY --from=build-env /image-builder/src/out ./

ENTRYPOINT ["dotnet", "/image-builder/Microsoft.DotNet.ImageBuilder.dll"]
