Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& ./../eng/docker-tools/build.ps1 `
    -Manifest src/manifest.json `
    -OptionalImageBuilderArgs "--var UniqueId=$(Get-Date -Format yyyyMMddHHmmss)" `
    -Paths "*"
