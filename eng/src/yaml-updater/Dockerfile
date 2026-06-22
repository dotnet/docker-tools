# This Dockerfile is intended to be built at the root of the repo.

# build image
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-env

WORKDIR /repo-src

# copy csproj and restore as distinct layers
COPY NuGet.config ./
COPY eng/src/file-pusher/*.csproj ./eng/src/file-pusher/
COPY eng/src/yaml-updater/*.csproj ./eng/src/yaml-updater/
COPY src/Automation/*.csproj ./src/Automation/
RUN dotnet restore ./eng/src/yaml-updater/yaml-updater.csproj

# copy everything else and build
COPY eng/src/file-pusher/. ./eng/src/file-pusher/
COPY eng/src/yaml-updater/. ./eng/src/yaml-updater/
COPY src/Automation/. ./src/Automation/
RUN dotnet publish ./eng/src/yaml-updater/yaml-updater.csproj -c Release -o out --no-restore


# runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine

# yaml-updater pushes changes using the git CLI
RUN apk add --no-cache git

# copy yaml-updater
WORKDIR /yaml-updater
COPY --from=build-env /repo-src/out ./

# copy repo
WORKDIR /repo
COPY . ./

ENTRYPOINT ["/yaml-updater/yaml-updater"]
