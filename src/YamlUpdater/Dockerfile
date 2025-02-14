# This Dockerfile is intended to be built at the root of the repo.

# build image
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-env

WORKDIR /src

# copy csproj and restore as distinct layers
COPY src/FilePusher/*.csproj ./file-pusher/
COPY src/YamlUpdater/*.csproj ./yaml-updater/
COPY NuGet.config ./

RUN dotnet restore ./yaml-updater/*.csproj

# copy everything else and build
COPY src/FilePusher/. ./file-pusher/
COPY src/YamlUpdater/. ./yaml-updater/
RUN dotnet publish ./yaml-updater/*.csproj -c Release -o out --no-restore


# runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine

# copy yaml-updater
WORKDIR /yaml-updater
COPY --from=build-env /src/out ./

# copy repo
WORKDIR /repo
COPY . ./

ENTRYPOINT ["/yaml-updater/YamlUpdater"]
