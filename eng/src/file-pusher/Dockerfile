# This Dockerfile is intended to be built at the root of the repo.

# build image
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-env

WORKDIR /repo-src

# copy csproj and restore as distinct layers
COPY NuGet.config ./
COPY eng/src/file-pusher/*.csproj ./eng/src/file-pusher/
COPY src/Automation/*.csproj ./src/Automation/
RUN dotnet restore ./eng/src/file-pusher/file-pusher.csproj

# copy everything else and build
COPY eng/src/file-pusher/. ./eng/src/file-pusher/
COPY src/Automation/. ./src/Automation/
RUN dotnet publish ./eng/src/file-pusher/file-pusher.csproj -c Release -o out --no-restore


# runtime image
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine

# file-pusher pushes changes using the git CLI
RUN apk add --no-cache git

# copy file-pusher
WORKDIR /file-pusher
COPY --from=build-env /repo-src/out ./

# copy repo
WORKDIR /repo
COPY . ./

ENTRYPOINT ["/file-pusher/file-pusher"]
