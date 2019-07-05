# Manifest File Schema Reference

The manifest file is the primary source of metadata that drives the production of all .NET Docker images.  It describes various attributes of the Docker images that are to be produced by a given GitHub repo. .NET Docker's engineering system consumes this file in various ways as part of the automated build pipelines and other tools. It's intended to be product-agnostic meaning that it could be used to describe metadata for Docker image production of any product, not just .NET.

## Manifest Attributes

|Attribute|Type|Description|
|-|-|-|
|readmePath|string|Relative path to the GitHub readme Markdown file associated with the manifest. This readme file documents the overall set of Docker repositories described by the manifest.|
|registry|string|The location of the Docker registry where the images are to be published.|
|variables|object|A set of custom variables that can be referenced in various parts of the manifest. This provides a few benefits:<br><ol><li>allows a commmonly used value to be defined only once and referenced by its variable name many times<li>allows tools that consume the manifest file to provide a mechanism to dynamically override the value of these variables.</ol>Variables may be referenced in other parts of the manifest by using the following syntax: $(_VariableName_).|
|repos|array|The set of Docker repositories described by this manifest. See [Repositories](#Repositories).|

## Repositories

A repository object contains metadata about a target Docker repository and the images to be contained in it.

|Attribute|Type|Description|
|-|-|-|
|id|string|A unique identifier of the repo. This is purely within the context of the manifest and not exposed to Docker in any way.|
|name|string|The name of the Docker repository where the described images are to be published (example: dotnet/core/runtime).|
|readmePath|string|Relative path to the GitHub readme Markdown file associated with the repository. This readme file documents the set of Docker images for this repository.|
|mcrTagsMetadataTemplatePath|string|Relative path to the MCR tags template YAML file that is used by tooling to generate the tags section of the readme file.|
|images|array|The set of images contained in this repository. See [Images](#Images).|

## Images

An image object contains metadata about a specific Docker image.

|Attribute|Type|Description|
|-|-|-|
|sharedTags|object|The set of [tags](#Tags) that are shared amongst all platform-specific versions of the image. An example of a shared tag, including its repo name, is dotnet/core/runtime:2.2; running `docker pull mcr.microsoft.com/dotnet/core/runtime:2.2` on Windows will get me the default Windows-based tag whereas running it on Linux will get me the default Linux-based tag.|

## Platforms

A platform object contains metadata about a platform-specific version of an image and refers to the actual Dockerfile used to build the image.

|Attribute|Type|Description|
|-|-|-|
|architecture|string|The processor architecture associated with the image. Allowed values: arm, arm64, amd64.|
|variant|string|A label which further distinguishes the architecture when it contains variants. For example, the ARM architecture has variants named v6, v7, etc.|
|dockerfile|string|Relative path to the associated Dockerfile.|
|os|string|The generic name of the operating system associated with the image. Allowed values: Linux, Windows.|
|osVersion|string|The specific version of the operating system associated with the image. Examples: alpine3.9, bionic, nanoserver-1903.|
|tags|object|The set of platform-specific tags associated with the image.  See [Tags](#Tags).
|customBuildLegGrouping|array|See [Custom Build Leg Grouping](#Custom-Build-Leg-Grouping).

## Tags

A tag object contains metadata about a Docker tag. It is a JSON object with its tag name used as the attribute name. Example:

```
"2.1": {
  "documentationGroup": "2.1"
}
```

|Attribute|Type|Description|
|-|-|-|
|documentationGroup|string|An identifier used to conceptually group related tags in the readme documentation.|
|isLocal|Boolean|Indicates whether the image should only be tagged with this tag on the local machine that builds the image. The published image will not include this tag. This is only used for advanced build dependency scenarios.|
|isUndocumented|Boolean|Indicates whether this tag should not be documented in the readme file. The image will still be tagged with this tag however. This is useful when deprecating a tag that still needs to be kept up-to-date but not wanting it documented.|

## Custom Build Leg Grouping

This object describes the tag dependencies of the image for a specific named scenario. This is for advanced cases only. It allows tooling to modify the build matrix that would normally be generated for the image by including the customizations described in this metadata. An example usage of this is in PR builds where it is necessary to build and test in the same job. In such a scenario, some images are part of a test matrix that require images to be available on the build machine that aren't part of that images dependency graph in normal scenarios. By specifying a customBuildLegGrouping for this scenario, those additional image dependencies can be specified and the build pipeline can make use of them when constructing its build graph when specified to do so.

|Attribute|Type|Description|
|-|-|-|
|name|string|Name of the grouping. This is just a custom label that can then be used by tooling to lookup the grouping when necessary.|
|dependencies|array|The set of image tags that this image is dependent upon for this scenario.|
