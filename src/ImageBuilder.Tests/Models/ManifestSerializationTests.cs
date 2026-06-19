// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="Manifest"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
[TestClass]
public class ManifestSerializationTests
{
    [TestMethod]
    public void DefaultManifest_Bidirectional()
    {
        Manifest manifest = new();

        // Nulls are omitted; empty Repos array is omitted (IList)
        // Empty Variables dictionary is NOT omitted (dictionaries are not IList)
        string json = """
            {
              "variables": {}
            }
            """;

        AssertBidirectional(manifest, json, AssertManifestsEqual);
    }

    [TestMethod]
    public void DefaultManifest_RoundTrip()
    {
        AssertRoundTrip(new Manifest(), AssertManifestsEqual);
    }

    [TestMethod]
    public void FullyPopulatedManifest_Bidirectional()
    {
        Manifest manifest = new()
        {
            Includes = ["include1.json", "include2.json"],
            Readme = new Readme("README.md", "README.template.md"),
            Registry = "mcr.microsoft.com",
            ImageInfo = new ImageInfoArtifact
            {
                Repo = "dotnet/versions",
                Tags = new Dictionary<string, Tag>
                {
                    ["latest"] = new()
                }
            },
            Repos = [],
            Variables = new Dictionary<string, string>
            {
                ["version"] = "8.0",
                ["osVersion"] = "jammy"
            }
        };

        // Empty Repos array is omitted (not required)
        string json = """
            {
              "includes": [
                "include1.json",
                "include2.json"
              ],
              "readme": {
                "path": "README.md",
                "templatePath": "README.template.md"
              },
              "registry": "mcr.microsoft.com",
              "imageInfo": {
                "repo": "dotnet/versions",
                "tags": {
                  "latest": {}
                }
              },
              "variables": {
                "version": "8.0",
                "osVersion": "jammy"
              }
            }
            """;

        AssertBidirectional(manifest, json, AssertManifestsEqual);
    }

    [TestMethod]
    public void FullyPopulatedManifest_RoundTrip()
    {
        Manifest manifest = new()
        {
            Includes = ["include1.json"],
            Readme = new Readme("README.md", null),
            Registry = "mcr.microsoft.com",
            Repos = [],
            Variables = new Dictionary<string, string> { ["key"] = "value" }
        };

        AssertRoundTrip(manifest, AssertManifestsEqual);
    }

    private static void AssertManifestsEqual(Manifest expected, Manifest actual)
    {
        actual.Includes.ShouldBe(expected.Includes);
        actual.Registry.ShouldBe(expected.Registry);
        AssertImageInfosEqual(expected.ImageInfo, actual.ImageInfo);
        (actual.Repos?.Length ?? 0).ShouldBe(expected.Repos?.Length ?? 0);
        actual.Variables.ShouldBe(expected.Variables);

        if (expected.Readme is null)
        {
            actual.Readme.ShouldBeNull();
        }
        else
        {
            actual.Readme.ShouldNotBeNull();
            actual.Readme.Path.ShouldBe(expected.Readme.Path);
            actual.Readme.TemplatePath.ShouldBe(expected.Readme.TemplatePath);
        }
    }

    private static void AssertImageInfosEqual(ImageInfoArtifact expected, ImageInfoArtifact actual)
    {
        if (expected is null)
        {
            actual.ShouldBeNull();
            return;
        }

        actual.ShouldNotBeNull();
        actual.Repo.ShouldBe(expected.Repo);
        actual.Tags.Count.ShouldBe(expected.Tags.Count);

        foreach ((string tagName, Tag expectedTag) in expected.Tags)
        {
            actual.Tags.ContainsKey(tagName).ShouldBeTrue();
            AssertTagsEqual(expectedTag, actual.Tags[tagName]);
        }
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
