# Installer image
FROM arm32v7/buildpack-deps:noble-curl AS installer

# Install .NET SDK
RUN curl --fail --show-error --location \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-arm.tar.gz \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-arm.tar.gz.sha512 \
    && sha512sum -c dotnet-sdk-10.0.403-linux-arm.tar.gz.sha512 \
    && mkdir --parents /dotnet \
    && tar --gzip --extract --no-same-owner --file dotnet-sdk-10.0.403-linux-arm.tar.gz --directory /dotnet ./dnx ./packs ./sdk ./sdk-manifests ./templates ./LICENSE.txt ./ThirdPartyNotices.txt \
    && rm \
        dotnet-sdk-10.0.403-linux-arm.tar.gz \
        dotnet-sdk-10.0.403-linux-arm.tar.gz.sha512

# Download PowerShell
RUN curl --fail --show-error --location --output PowerShell.Linux.arm32.7.6.9.nupkg https://powershellinfraartifacts-gkhedzdeaghdezhr.z01.azurefd.net/tool/7.6.9/PowerShell.Linux.arm32.7.6.9.nupkg \
    && echo "157b5dc526e2a4664831f308ab3cffd87dcf34123dec03dd6f3e1b7dcd76e0c8  PowerShell.Linux.arm32.7.6.9.nupkg" | sha256sum -c -


# .NET SDK image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble-arm32v7

ENV \
    # Do not generate certificate
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    # Do not show first run text
    DOTNET_NOLOGO=true \
    # SDK version
    DOTNET_SDK_VERSION=10.0.403 \
    # Enable correct mode for dotnet watch (only mode supported in a container)
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    # Skip extraction of XML docs - generally not useful within an image/container - helps performance
    NUGET_XMLDOC_MODE=skip \
    # PowerShell telemetry for docker image usage
    POWERSHELL_DISTRIBUTION_CHANNEL=PSDocker-DotnetSDK-Ubuntu-24.04

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        git \
        libatomic1 \
        wget \
    && rm -rf /var/lib/apt/lists/*

COPY --from=installer ["/dotnet", "/usr/share/dotnet"]

COPY --from=installer ["/PowerShell.Linux.arm32.7.6.9.nupkg", "/"]

RUN ln -s /usr/share/dotnet/dnx /usr/bin/dnx \
    # Trigger first run experience by running arbitrary cmd
    && dotnet help

# Install PowerShell global tool
RUN mkdir --parents /usr/share/powershell \
    && dotnet tool install --add-source / --tool-path /usr/share/powershell --version 7.6.9 PowerShell.Linux.arm32 \
    && dotnet nuget locals all --clear \
    && rm PowerShell.Linux.arm32.7.6.9.nupkg \
    && ln -s /usr/share/powershell/pwsh /usr/bin/pwsh \
    && chmod 755 /usr/share/powershell/pwsh \
    && chmod 755 /usr/share/powershell/.store/powershell.linux.arm32/7.6.9/powershell.linux.arm32/7.6.9/tools/*/any/pwsh \
    # To reduce image size, remove the copy nupkg that nuget keeps.
    && find /usr/share/powershell -print | grep -i '.*[.]nupkg$' | xargs rm
