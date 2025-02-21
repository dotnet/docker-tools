Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& ./../../eng/common/build.ps1 `
    -Manifest src/Microsoft.DotNet.ImageBuilder/manifest.json `
    -OptionalImageBuilderArgs "--var UniqueId=$(Get-Date -Format yyyyMMddHHmmss)" `
    -Paths "*"
