#!/usr/bin/env pwsh
# Generates this sample repo's Dockerfiles from its templates with TemplateTool.

$ErrorActionPreference = 'Stop'

dotnet run --project "$PSScriptRoot/../TemplateTool/TemplateTool.csproj" -- "$PSScriptRoot/manifest.yaml"
exit $LASTEXITCODE
