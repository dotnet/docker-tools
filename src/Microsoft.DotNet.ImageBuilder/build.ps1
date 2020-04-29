[cmdletbinding()]
param(
    [string]$DockerRepo = "mcr.microsoft.com/dotnet-buildtools/image-builder",
    [switch]$PushImages,
    [switch]$CleanupDocker,
    [string]$TagTimestamp = (Get-Date -Format yyyyMMddHHmmss)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-CleanupDocker($ActiveOS)
{
    if ($CleanupDocker) {
        if ("$ActiveOS" -eq "windows") {
            # Windows base images are large, preserve them to avoid the overhead of pulling each time.
            docker images |
                Where-Object {
                    -Not ($_.StartsWith("mcr.microsoft.com/windows")`
                        -Or $_.StartsWith("REPOSITORY ")) } |
                ForEach-Object { $_.Split(' ', [System.StringSplitOptions]::RemoveEmptyEntries)[2] } |
                Select-Object -Unique |
                ForEach-Object { docker rmi -f $_ }
        }
        else {
            docker system prune -a -f
        }
    }
}

$(docker version) | % { Write-Host "$_" }
$activeOS = docker version -f "{{ .Server.Os }}"
Invoke-CleanupDocker $activeOS

try {
    $stableTag = "$($DockerRepo):$activeOS-$TagTimestamp"
    $floatingTag = "image-builder"

    & docker build --pull -t $stableTag -t $floatingTag -f "$($PSScriptRoot)/Dockerfile.$activeOS" $PSScriptRoot
    if ($LastExitCode -ne 0) {
        throw "Failed building ImageBuilder"
    }

    if ($PushImages) {
        & docker push $stableTag
        if ($LastExitCode -ne 0) {
            throw "Failed pushing images"
        }
    }
}
finally {
    Invoke-CleanupDocker $activeOS
}
