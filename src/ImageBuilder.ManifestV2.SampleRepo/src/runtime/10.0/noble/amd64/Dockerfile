# Installer image
FROM amd64/buildpack-deps:noble-curl AS installer

# Retrieve .NET Runtime
RUN curl --fail --show-error --location \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-linux-x64.tar.gz \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-linux-x64.tar.gz.sha512 \
    && sha512sum -c dotnet-runtime-10.0.14-linux-x64.tar.gz.sha512 \
    && mkdir --parents /dotnet \
    && tar --gzip --extract --no-same-owner --file dotnet-runtime-10.0.14-linux-x64.tar.gz --directory /dotnet \
    && rm \
        dotnet-runtime-10.0.14-linux-x64.tar.gz \
        dotnet-runtime-10.0.14-linux-x64.tar.gz.sha512


# .NET runtime image
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0.14-noble-amd64

# .NET Runtime version
ENV DOTNET_VERSION=10.0.14

COPY --from=installer ["/dotnet", "/usr/share/dotnet"]

RUN ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet
