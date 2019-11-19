# Use this Dockerfile to create a Linux PowerShell runner image
# Usage: docker run --rm -v /var/run/docker.sock:/var/run/docker.sock runner pwsh -File build.ps1 <params....>

FROM microsoft/dotnet-buildtools-prereqs:debian-stretch-docker-testrunner-63f2145-20184325094343

WORKDIR /repo
COPY . .
