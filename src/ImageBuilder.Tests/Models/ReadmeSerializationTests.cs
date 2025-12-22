// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="Readme"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class ReadmeSerializationTests
{
    [Fact]
    public void DefaultReadme_Bidirectional()
    {
        Readme readme = new();

        // Path is required so always serialized; null templatePath is omitted
        string json = """
            {
              "path": ""
            }
            """;

        AssertBidirectional(readme, json, AssertReadmesEqual);
    }

    [Fact]
    public void DefaultReadme_RoundTrip()
    {
        AssertRoundTrip(new Readme(), AssertReadmesEqual);
    }

    [Fact]
    public void FullyPopulatedReadme_Bidirectional()
    {
        Readme readme = new()
        {
            Path = "README.md",
            TemplatePath = "README.template.md"
        };

        string json = """
            {
              "path": "README.md",
              "templatePath": "README.template.md"
            }
            """;

        AssertBidirectional(readme, json, AssertReadmesEqual);
    }

    [Fact]
    public void FullyPopulatedReadme_RoundTrip()
    {
        Readme readme = new()
        {
            Path = "README.md",
            TemplatePath = "README.template.md"
        };

        AssertRoundTrip(readme, AssertReadmesEqual);
    }

    [Fact]
    public void ConstructorWithParameters_Bidirectional()
    {
        Readme readme = new("docs/README.md", "docs/README.template.md");

        string json = """
            {
              "path": "docs/README.md",
              "templatePath": "docs/README.template.md"
            }
            """;

        AssertBidirectional(readme, json, AssertReadmesEqual);
    }

    [Fact]
    public void ConstructorWithNullTemplatePath_Bidirectional()
    {
        Readme readme = new("README.md", null);

        // Null templatePath is omitted
        string json = """
            {
              "path": "README.md"
            }
            """;

        AssertBidirectional(readme, json, AssertReadmesEqual);
    }

    [Fact]
    public void Deserialization_PathIsRequired_Missing()
    {
        // Path has [JsonProperty(Required = Required.Always)]
        // Deserialization should fail when Path is missing
        string json = """
            {
              "templatePath": "README.template.md"
            }
            """;

        AssertDeserializationFails<Readme>(json, nameof(Readme.Path));
    }

    [Fact]
    public void Deserialization_PathIsRequired_Null()
    {
        // Path has [JsonProperty(Required = Required.Always)]
        // Deserialization should fail when Path is explicitly null
        string json = """
            {
              "path": null,
              "templatePath": "README.template.md"
            }
            """;

        AssertDeserializationFails<Readme>(json, nameof(Readme.Path));
    }

    [Fact]
    public void Deserialization_TemplatePathIsOptional()
    {
        // TemplatePath is optional (nullable, no Required attribute)
        string json = """
            {
              "path": "README.md"
            }
            """;

        Readme expected = new()
        {
            Path = "README.md",
            TemplatePath = null
        };

        AssertDeserialization(json, expected, AssertReadmesEqual);
    }

    private static void AssertReadmesEqual(Readme expected, Readme actual)
    {
        Assert.Equal(expected.Path, actual.Path);
        Assert.Equal(expected.TemplatePath, actual.TemplatePath);
    }
}
