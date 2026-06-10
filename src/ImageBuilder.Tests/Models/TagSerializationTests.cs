// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="Tag"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
[TestClass]
public class TagSerializationTests
{
    [TestMethod]
    public void DefaultTag_Bidirectional()
    {
        Tag tag = new();

        // All properties have default/null values, so they are omitted
        string json = "{}";

        AssertBidirectional(tag, json, AssertTagsEqual);
    }

    [TestMethod]
    public void DefaultTag_RoundTrip()
    {
        AssertRoundTrip(new Tag(), AssertTagsEqual);
    }

    [TestMethod]
    public void FullyPopulatedTag_Bidirectional()
    {
        Tag tag = new()
        {
            DocumentationGroup = "test-group",
            DocType = TagDocumentationType.Undocumented,
            Syndication = new TagSyndication
            {
                Repo = "target-repo",
                DestinationTags = ["tag1", "tag2"]
            }
        };

        // Enums serialize as strings with StringEnumConverter.
        string json = """
            {
              "documentationGroup": "test-group",
              "docType": "Undocumented",
              "syndication": {
                "repo": "target-repo",
                "destinationTags": [
                  "tag1",
                  "tag2"
                ]
              }
            }
            """;

        AssertBidirectional(tag, json, AssertTagsEqual);
    }

    [TestMethod]
    public void FullyPopulatedTag_RoundTrip()
    {
        Tag tag = new()
        {
            DocumentationGroup = "test-group",
            DocType = TagDocumentationType.Undocumented,
            Syndication = new TagSyndication
            {
                Repo = "target-repo",
                DestinationTags = ["tag1", "tag2"]
            }
        };

        AssertRoundTrip(tag, AssertTagsEqual);
    }

    private static void AssertTagsEqual(Tag expected, Tag actual)
    {
        actual.DocumentationGroup.ShouldBe(expected.DocumentationGroup);
        actual.DocType.ShouldBe(expected.DocType);

        if (expected.Syndication is null)
        {
            actual.Syndication.ShouldBeNull();
        }
        else
        {
            actual.Syndication.ShouldNotBeNull();
            actual.Syndication.Repo.ShouldBe(expected.Syndication.Repo);
            actual.Syndication.DestinationTags.ShouldBe(expected.Syndication.DestinationTags);
        }
    }
}
