// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="ImageInfoArtifact"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
[TestClass]
public class ImageInfoArtifactSerializationTests
{
    [TestMethod]
    public void DefaultImageInfoArtifact_Bidirectional()
    {
        ImageInfoArtifact imageInfo = new();

        // Required properties are serialized even when empty.
        string json = """
            {
              "repo": "",
              "tags": {}
            }
            """;

        AssertBidirectional(imageInfo, json, AssertImageInfosEqual);
    }

    [TestMethod]
    public void FullyPopulatedImageInfoArtifact_Bidirectional()
    {
        ImageInfoArtifact imageInfo = new()
        {
            Repo = "dotnet/versions",
            Tags = new Dictionary<string, Tag>
            {
                ["latest"] = new()
                {
                    DocumentationGroup = "versions"
                }
            }
        };

        string json = """
            {
              "repo": "dotnet/versions",
              "tags": {
                "latest": {
                  "documentationGroup": "versions"
                }
              }
            }
            """;

        AssertBidirectional(imageInfo, json, AssertImageInfosEqual);
    }

    [TestMethod]
    public void FullyPopulatedImageInfoArtifact_RoundTrip()
    {
        ImageInfoArtifact imageInfo = new()
        {
            Repo = "dotnet/versions",
            Tags = new Dictionary<string, Tag>
            {
                ["latest"] = new()
            }
        };

        AssertRoundTrip(imageInfo, AssertImageInfosEqual);
    }

    [TestMethod]
    public void MissingRepo_DeserializationFails()
    {
        string json = """
            {
              "tags": {}
            }
            """;

        AssertDeserializationFails<ImageInfoArtifact>(json, nameof(ImageInfoArtifact.Repo));
    }

    [TestMethod]
    public void NullRepo_DeserializationFails()
    {
        string json = """
            {
              "repo": null,
              "tags": {}
            }
            """;

        AssertDeserializationFails<ImageInfoArtifact>(json, nameof(ImageInfoArtifact.Repo));
    }

    [TestMethod]
    public void MissingTags_DeserializationFails()
    {
        string json = """
            {
              "repo": "dotnet/versions"
            }
            """;

        AssertDeserializationFails<ImageInfoArtifact>(json, nameof(ImageInfoArtifact.Tags));
    }

    [TestMethod]
    public void NullTags_DeserializationFails()
    {
        string json = """
            {
              "repo": "dotnet/versions",
              "tags": null
            }
            """;

        AssertDeserializationFails<ImageInfoArtifact>(json, nameof(ImageInfoArtifact.Tags));
    }

    private static void AssertImageInfosEqual(ImageInfoArtifact expected, ImageInfoArtifact actual)
    {
        actual.Repo.ShouldBe(expected.Repo);
        actual.Tags.Count.ShouldBe(expected.Tags.Count);

        foreach ((string tagName, Tag expectedTag) in expected.Tags)
        {
            actual.Tags.ContainsKey(tagName).ShouldBeTrue();
            actual.Tags[tagName].DocumentationGroup.ShouldBe(expectedTag.DocumentationGroup);
            actual.Tags[tagName].DocType.ShouldBe(expectedTag.DocType);
            actual.Tags[tagName].Syndication.ShouldBe(expectedTag.Syndication);
        }
    }
}
