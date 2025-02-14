Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& $PSScriptRoot/../../eng/common/build.ps1 `
    -Manifest manifest.json `
    -OptionalImageBuilderArgs "--var UniqueId=$(Get-Date -Format yyyyMMddHHmmss)" `
    -Paths "*"
