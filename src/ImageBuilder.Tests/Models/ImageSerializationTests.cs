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
/// Serialization and deserialization tests for <see cref="Image"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class ImageSerializationTests
{
    [Fact]
    public void DefaultImage_CannotSerialize()
    {
        // A default Image has null Platforms, which violates
        // [JsonProperty(Required = Required.Always)] and cannot be serialized.
        Image image = new();

        AssertSerializationFails(image, nameof(Image.Platforms));
    }

    [Fact]
    public void FullyPopulatedImage_Serialization()
    {
        // Bidirectional test not possible: empty Platforms array is required for deserialization
        // but omitted during serialization
        Image image = new()
        {
            Platforms = [], // Leave sub-model arrays empty per instructions
            SharedTags = new Dictionary<string, Tag>
            {
                ["8.0"] = new Tag(),
                ["latest"] = new Tag { DocType = TagDocumentationType.Undocumented }
            },
            ProductVersion = "8.0.0"
        };

        // Empty Platforms array is omitted by JsonHelper.CustomContractResolver
        string json = """
            {
              "sharedTags": {
                "8.0": {
                  "documentationGroup": null,
                  "docType": "Documented",
                  "syndication": null
                },
                "latest": {
                  "documentationGroup": null,
                  "docType": "Undocumented",
                  "syndication": null
                }
              },
              "productVersion": "8.0.0"
            }
            """;

        AssertSerialization(image, json);
    }

    [Fact]
    public void FullyPopulatedImage_RoundTrip()
    {
        // RoundTrip tests fail when arrays are empty because they get omitted on serialization
        // but are required on deserialization. Test serialization only.
        Image image = new()
        {
            Platforms = [],
            SharedTags = new Dictionary<string, Tag> { ["8.0"] = new Tag() },
            ProductVersion = "8.0.0"
        };

        // Cannot round-trip because empty Platforms array is omitted but required
        string json = """
            {
              "sharedTags": {
                "8.0": {
                  "documentationGroup": null,
                  "docType": "Documented",
                  "syndication": null
                }
              },
              "productVersion": "8.0.0"
            }
            """;

        AssertSerialization(image, json);
    }

    [Fact]
    public void MinimalImage_Serialization()
    {
        // Bidirectional test not possible: empty Platforms array is required for deserialization
        // but omitted during serialization
        Image image = new()
        {
            Platforms = []
        };

        // Empty Platforms array is omitted by JsonHelper.CustomContractResolver
        string json = """
            {
              "sharedTags": null,
              "productVersion": null
            }
            """;

        AssertSerialization(image, json);
    }

    [Fact]
    public void Deserialization_PlatformsIsRequired_Missing()
    {
        string json = """
            {
              "productVersion": "8.0.0"
            }
            """;

        AssertDeserializationFails<Image>(json, nameof(Image.Platforms));
    }

    [Fact]
    public void Deserialization_PlatformsIsRequired_Null()
    {
        string json = """
            {
              "platforms": null,
              "productVersion": "8.0.0"
            }
            """;

        AssertDeserializationFails<Image>(json, nameof(Image.Platforms));
    }

    [Fact]
    public void Deserialization_SharedTagsIsOptional()
    {
        string json = """
            {
              "platforms": []
            }
            """;

        Image expected = new()
        {
            Platforms = [],
            SharedTags = null
        };

        AssertDeserialization(json, expected, AssertImagesEqual);
    }

    private static void AssertImagesEqual(Image expected, Image actual)
    {
        Assert.Equal(expected.Platforms?.Length ?? 0, actual.Platforms?.Length ?? 0);
        Assert.Equal(expected.ProductVersion, actual.ProductVersion);

        if (expected.SharedTags is null)
        {
            Assert.Null(actual.SharedTags);
        }
        else
        {
            Assert.NotNull(actual.SharedTags);
            Assert.Equal(expected.SharedTags.Count, actual.SharedTags.Count);
            foreach (string key in expected.SharedTags.Keys)
            {
                Assert.True(actual.SharedTags.ContainsKey(key));
            }
        }
    }
}
