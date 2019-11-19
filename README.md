# docker-tools
This is a repo to house some common tools for use in the various .NET Docker repos. 

# Image Builder
A tool used to build and publish Docker images.

The Image Builder tool can be acquired via a Docker image available at [mcr.microsoft.com/dotnet-buildtools/image-builder](https://mcr.microsoft.com/v2/dotnet-buildtools/image-builder/tags/list) or built from source via the [build script](./src/Microsoft.DotNet.ImageBuilder/build.ps1).

The Image Builder tool relies on metadata which defines various information needed to build and tag Docker images.  The metadata is stored in a manifest.json file ([sample](https://github.com/dotnet/dotnet-docker/blob/master/manifest.json)).  The metadata schema is defined in [source](./src/Microsoft.DotNet.ImageBuilder/src/Model).

The full list of supported commands can be seen by running the tool.

- Linux container environment: `docker run -it --rm -v /var/run/docker.sock:/var/run/docker.sock mcr.microsoft.com/dotnet-buildtools/image-builder:debian-20190223173930 -h`

The list of support command options can be seen by specifying the `-h` command option.  The following illustrates how to list the build options.

- Linux container environment: `docker run -it --rm -v /var/run/docker.sock:/var/run/docker.sock mcr.microsoft.com/dotnet-buildtools/image-builder:debian-20190223173930 build -h` 
