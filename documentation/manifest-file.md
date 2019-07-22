# Manifest File

The manifest file is the primary source of metadata that drives the production of all .NET Docker images.  It describes various attributes of the Docker images that are to be produced by a given GitHub repo. .NET Docker's engineering system consumes this file in various ways as part of the automated build pipelines and other tools. It's intended to be product-agnostic meaning that it could be used to describe metadata for Docker image production of any product, not just .NET.

For a description of the schema, see the source location at https://github.com/dotnet/docker-tools/tree/master/Microsoft.DotNet.ImageBuilder/src/Models/Manifest. Alternatively, you can generate a JSON schema of the manifest by using the ImageBuilder tool:
```
Microsoft.DotNet.ImageBuilder.exe showManifestSchema
```
