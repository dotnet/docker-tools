# Installer image
FROM arm32v7/buildpack-deps:noble-curl AS installer

# Retrieve ASP.NET Core
RUN curl --fail --show-error --location \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.14/aspnetcore-runtime-10.0.14-linux-arm.tar.gz \
        --remote-name https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.14/aspnetcore-runtime-10.0.14-linux-arm.tar.gz.sha512 \
    && sha512sum -c aspnetcore-runtime-10.0.14-linux-arm.tar.gz.sha512 \
    && mkdir --parents /dotnet \
    && tar --gzip --extract --no-same-owner --file aspnetcore-runtime-10.0.14-linux-arm.tar.gz --directory /dotnet ./shared/Microsoft.AspNetCore.App \
    && rm \
        aspnetcore-runtime-10.0.14-linux-arm.tar.gz \
        aspnetcore-runtime-10.0.14-linux-arm.tar.gz.sha512


# ASP.NET Core image
FROM mcr.microsoft.com/dotnet/runtime:10.0.14-noble-arm32v7

# ASP.NET Core version
ENV ASPNET_VERSION=10.0.14

COPY --from=installer ["/dotnet", "/usr/share/dotnet"]
