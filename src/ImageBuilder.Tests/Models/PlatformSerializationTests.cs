// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="Platform"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class PlatformSerializationTests
{
    [Fact]
    public void DefaultPlatform_Bidirectional()
    {
        // A default Platform can be serialized - it has default values for required properties
        // Architecture defaults to AMD64 and is omitted due to DefaultValueHandling.IgnoreAndPopulate
        Platform platform = new();

        // Empty BuildArgs dictionary IS serialized (only IList is omitted)
        // Empty Tags dictionary IS serialized (required)
        // Empty arrays (CustomBuildLegGroups, Images) are omitted
        string json = """
            {
              "buildArgs": {},
              "dockerfile": "",
              "os": "Linux",
              "osVersion": "",
              "tags": {}
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformsEqual);
    }

    [Fact]
    public void FullyPopulatedPlatform_Bidirectional()
    {
        Platform platform = new()
        {
            Architecture = Architecture.ARM64,
            BuildArgs = new Dictionary<string, string>
            {
                ["DOTNET_VERSION"] = "8.0.0",
                ["ASPNET_VERSION"] = "8.0.0"
            },
            Dockerfile = "src/runtime/8.0/jammy/arm64v8/Dockerfile",
            DockerfileTemplate = "src/runtime/Dockerfile.linux.template",
            OS = OS.Linux,
            OsVersion = "jammy",
            Tags = new Dictionary<string, Tag>
            {
                ["8.0-jammy-arm64v8"] = new Tag(),
                ["8.0"] = new Tag { DocType = TagDocumentationType.Undocumented }
            },
            CustomBuildLegGroups = [], // Leave sub-model arrays empty per instructions
            Variant = "v8"
        };

        // Empty CustomBuildLegGroups is omitted; default Tag properties are omitted
        string json = """
            {
              "architecture": "ARM64",
              "buildArgs": {
                "DOTNET_VERSION": "8.0.0",
                "ASPNET_VERSION": "8.0.0"
              },
              "dockerfile": "src/runtime/8.0/jammy/arm64v8/Dockerfile",
              "dockerfileTemplate": "src/runtime/Dockerfile.linux.template",
              "os": "Linux",
              "osVersion": "jammy",
              "tags": {
                "8.0-jammy-arm64v8": {},
                "8.0": {
                  "docType": "Undocumented"
                }
              },
              "variant": "v8"
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformsEqual);
    }

    [Fact]
    public void FullyPopulatedPlatform_RoundTrip()
    {
        Platform platform = new()
        {
            Architecture = Architecture.ARM,
            BuildArgs = new Dictionary<string, string> { ["VERSION"] = "8.0" },
            Dockerfile = "src/Dockerfile",
            DockerfileTemplate = "src/Dockerfile.template",
            OS = OS.Linux,
            OsVersion = "alpine3.18",
            Tags = new Dictionary<string, Tag> { ["latest"] = new Tag() },
            CustomBuildLegGroups = [],
            Variant = "v7"
        };

        AssertRoundTrip(platform, AssertPlatformsEqual);
    }

    [Fact]
    public void MinimalPlatform_Bidirectional()
    {
        // Platform with only required properties, using default Architecture
        Platform platform = new()
        {
            Dockerfile = "src/Dockerfile",
            OS = OS.Linux,
            OsVersion = "jammy",
            Tags = new Dictionary<string, Tag> { ["8.0"] = new Tag() }
        };

        // Architecture defaults to AMD64 and is not serialized due to DefaultValueHandling.IgnoreAndPopulate
        // Empty dictionaries ARE serialized (only IList is omitted)
        // Empty CustomBuildLegGroups array is omitted; default Tag properties are omitted
        string json = """
            {
              "buildArgs": {},
              "dockerfile": "src/Dockerfile",
              "os": "Linux",
              "osVersion": "jammy",
              "tags": {
                "8.0": {}
              }
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformsEqual);
    }

    [Fact]
    public void WindowsPlatform_Bidirectional()
    {
        Platform platform = new()
        {
            Architecture = Architecture.AMD64,
            Dockerfile = "src/runtime/8.0/nanoserver-ltsc2022/amd64/Dockerfile",
            OS = OS.Windows,
            OsVersion = "nanoserver-ltsc2022",
            Tags = new Dictionary<string, Tag> { ["8.0-nanoserver-ltsc2022"] = new Tag() }
        };

        // Architecture is AMD64 (default) so it's omitted
        // Empty BuildArgs dictionary IS serialized (only IList is omitted)
        // Empty CustomBuildLegGroups array is omitted; default Tag properties are omitted
        string json = """
            {
              "buildArgs": {},
              "dockerfile": "src/runtime/8.0/nanoserver-ltsc2022/amd64/Dockerfile",
              "os": "Windows",
              "osVersion": "nanoserver-ltsc2022",
              "tags": {
                "8.0-nanoserver-ltsc2022": {}
              }
            }
            """;

        AssertBidirectional(platform, json, AssertPlatformsEqual);
    }

    [Fact]
    public void Deserialization_DockerfileIsRequired_Missing()
    {
        string json = """
            {
              "os": "Linux",
              "osVersion": "jammy",
              "tags": {}
            }
            """;

        AssertDeserializationFails<Platform>(json, nameof(Platform.Dockerfile));
    }

    [Fact]
    public void Deserialization_OsIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "osVersion": "jammy",
              "tags": {}
            }
            """;

        AssertDeserializationFails<Platform>(json, nameof(Platform.OS));
    }

    [Fact]
    public void Deserialization_OsVersionIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "os": "Linux",
              "tags": {}
            }
            """;

        AssertDeserializationFails<Platform>(json, nameof(Platform.OsVersion));
    }

    [Fact]
    public void Deserialization_TagsIsRequired_Missing()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "os": "Linux",
              "osVersion": "jammy"
            }
            """;

        AssertDeserializationFails<Platform>(json, nameof(Platform.Tags));
    }

    [Fact]
    public void Deserialization_TagsIsRequired_Null()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "os": "Linux",
              "osVersion": "jammy",
              "tags": null
            }
            """;

        AssertDeserializationFails<Platform>(json, nameof(Platform.Tags));
    }

    [Fact]
    public void Deserialization_ArchitectureDefaultsToAMD64()
    {
        string json = """
            {
              "dockerfile": "src/Dockerfile",
              "os": "Linux",
              "osVersion": "jammy",
              "tags": {}
            }
            """;

        Platform expected = new()
        {
            Architecture = Architecture.AMD64,
            Dockerfile = "src/Dockerfile",
            OS = OS.Linux,
            OsVersion = "jammy",
            Tags = new Dictionary<string, Tag>()
        };

        AssertDeserialization(json, expected, AssertPlatformsEqual);
    }

    private static void AssertPlatformsEqual(Platform expected, Platform actual)
    {
        Assert.Equal(expected.Architecture, actual.Architecture);
        Assert.Equal(expected.BuildArgs, actual.BuildArgs);
        Assert.Equal(expected.Dockerfile, actual.Dockerfile);
        Assert.Equal(expected.DockerfileTemplate, actual.DockerfileTemplate);
        Assert.Equal(expected.OS, actual.OS);
        Assert.Equal(expected.OsVersion, actual.OsVersion);
        Assert.Equal(expected.Tags?.Count ?? 0, actual.Tags?.Count ?? 0);
        Assert.Equal(expected.CustomBuildLegGroups?.Length ?? 0, actual.CustomBuildLegGroups?.Length ?? 0);
        Assert.Equal(expected.Variant, actual.Variant);
    }
}
