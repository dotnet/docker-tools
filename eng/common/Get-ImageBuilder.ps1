#!/usr/bin/env pwsh

# Load common image names
Get-Content $PSScriptRoot/templates/variables/docker-images.yml |
Where-Object { $_.Trim().Length -gt 0 -and $_.Trim() -notlike 'variables:' -and $_.Trim() -notlike '# *' } |
ForEach-Object { 
    $parts = $_.Split(':', 2)
    Set-Variable -Name $parts[0].Trim() -Value $parts[1].Trim() -Scope Global
}

& docker inspect ${imageNames.imagebuilderName} | Out-Null
if (-not $?) {
    Write-Output "Pulling"
    & $PSScriptRoot/Invoke-WithRetry.ps1 "docker pull ${imageNames.imagebuilderName}"
}
