# build Microsoft.DotNet.ImageBuilder
FROM mcr.microsoft.com/dotnet/core/sdk:2.1 AS build-env
WORKDIR /image-builder

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
RUN dotnet publish ./src/Microsoft.DotNet.ImageBuilder.csproj -c Release -o out -r win7-x64


# build runtime image
FROM mcr.microsoft.com/windows/nanoserver:sac2016
WORKDIR /image-builder
COPY --from=build-env /image-builder/src/out ./
