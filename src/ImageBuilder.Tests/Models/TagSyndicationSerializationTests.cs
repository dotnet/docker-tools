// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="TagSyndication"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
[TestClass]
public class TagSyndicationSerializationTests
{
    [TestMethod]
    public void DefaultTagSyndication_Bidirectional()
    {
        TagSyndication syndication = new();

        // All properties are null, so they are omitted
        string json = "{}";

        AssertBidirectional(syndication, json, AssertTagSyndicationsEqual);
    }

    [TestMethod]
    public void DefaultTagSyndication_RoundTrip()
    {
        AssertRoundTrip(new TagSyndication(), AssertTagSyndicationsEqual);
    }

    [TestMethod]
    public void FullyPopulatedTagSyndication_Bidirectional()
    {
        TagSyndication syndication = new()
        {
            Repo = "dotnet/core/runtime",
            DestinationTags = ["8.0", "8.0.0", "latest"]
        };

        string json = """
            {
              "repo": "dotnet/core/runtime",
              "destinationTags": [
                "8.0",
                "8.0.0",
                "latest"
              ]
            }
            """;

        AssertBidirectional(syndication, json, AssertTagSyndicationsEqual);
    }

    [TestMethod]
    public void FullyPopulatedTagSyndication_RoundTrip()
    {
        TagSyndication syndication = new()
        {
            Repo = "target-repo",
            DestinationTags = ["tag1", "tag2"]
        };

        AssertRoundTrip(syndication, AssertTagSyndicationsEqual);
    }

    [TestMethod]
    public void TagSyndicationWithRepoOnly_Bidirectional()
    {
        TagSyndication syndication = new()
        {
            Repo = "dotnet/runtime"
        };

        // Null destinationTags is omitted
        string json = """
            {
              "repo": "dotnet/runtime"
            }
            """;

        AssertBidirectional(syndication, json, AssertTagSyndicationsEqual);
    }

    [TestMethod]
    public void TagSyndicationWithDestinationTagsOnly_Bidirectional()
    {
        TagSyndication syndication = new()
        {
            DestinationTags = ["8.0", "latest"]
        };

        // Null repo is omitted
        string json = """
            {
              "destinationTags": [
                "8.0",
                "latest"
              ]
            }
            """;

        AssertBidirectional(syndication, json, AssertTagSyndicationsEqual);
    }

    private static void AssertTagSyndicationsEqual(TagSyndication expected, TagSyndication actual)
    {
        actual.Repo.ShouldBe(expected.Repo);
        actual.DestinationTags.ShouldBe(expected.DestinationTags);
    }
}
