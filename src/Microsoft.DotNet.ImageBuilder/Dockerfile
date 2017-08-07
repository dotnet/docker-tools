# Use this Dockerfile to create a runner image
#     docker build -t image-builder .
#     docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v <local path to build>:/repo -w /repo image-builder <image-build args>

FROM microsoft/dotnet:2.0-sdk

# Install Docker
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        apt-transport-https \
        software-properties-common \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://download.docker.com/linux/debian/gpg | apt-key add - \
    && add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/debian $(lsb_release -cs) stable" \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        docker-ce \
    && rm -rf /var/lib/apt/lists/*

# Install manifest-tool
RUN curl -fsSL "https://github.com/estesp/manifest-tool/releases/download/v0.6.0/manifest-tool-linux-amd64" \
        -o /usr/local/bin/manifest-tool \
    && chmod +x /usr/local/bin/manifest-tool

# Build Microsoft.DotNet.ImageBuilder
WORKDIR Microsoft.DotNet.ImageBuilder
COPY *.csproj .
COPY NuGet.config .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out

ENTRYPOINT ["dotnet", "/out/Microsoft.DotNet.ImageBuilder.dll"]
