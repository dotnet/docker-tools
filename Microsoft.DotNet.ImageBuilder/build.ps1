[cmdletbinding()]
param(
    [string]$DockerRepo = "microsoft/dotnet-buildtools-prereqs",
    [switch]$PushImages,
    [switch]$CleanupDocker
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
                    -Not ($_.StartsWith("microsoft/nanoserver ")`
                    -Or $_.StartsWith("microsoft/windowsservercore ")`
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
    if ($activeOS -eq "windows") {
        $osFlavor = "nanoserver"
    }
    else {
        $osFlavor = "debian"
    }

    $stableTag = "$($DockerRepo):image-builder-$osFlavor-$((Get-Date -Format yyyyMMddHHmmss).ToLower())"
    $floatingTag = "image-builder"

    & docker build -t $stableTag -t $floatingTag -f Dockerfile.$osFlavor .
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
