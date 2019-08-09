# This Dockerfile is intended to be build at the root of the repo.

# build image
FROM mcr.microsoft.com/dotnet/core/sdk:3.0-alpine AS build-env

WORKDIR /file-pusher

# copy csproj and restore as distinct layers
COPY eng/file-pusher/*.csproj ./
COPY eng/file-pusher/NuGet.config ./
RUN dotnet restore

# copy everything else and build
COPY eng/file-pusher/. ./
RUN dotnet publish -c Release -o out --no-restore


# runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.0-alpine

# copy file-pusher
WORKDIR /file-pusher
COPY --from=build-env /file-pusher/out ./

# copy repo
WORKDIR /repo
COPY . ./

ENTRYPOINT ["/file-pusher/file-pusher"]
