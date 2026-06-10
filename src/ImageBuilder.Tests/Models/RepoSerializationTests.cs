// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="Repo"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
[TestClass]
public class RepoSerializationTests
{
    [TestMethod]
    public void DefaultRepo_CannotSerialize()
    {
        // A default Repo has null Images and Name, which violate
        // [JsonProperty(Required = Required.Always)] and cannot be serialized.
        Repo repo = new();

        AssertSerializationFails(repo, nameof(Repo.Images));
    }

    [TestMethod]
    public void FullyPopulatedRepo_Bidirectional()
    {
        Repo repo = new()
        {
            Id = "runtime",
            Images = [],
            McrTagsMetadataTemplate = "tags-metadata-template.yaml",
            Name = "dotnet/runtime",
            Readmes = []
        };

        string json = """
            {
              "id": "runtime",
              "images": [],
              "mcrTagsMetadataTemplate": "tags-metadata-template.yaml",
              "name": "dotnet/runtime"
            }
            """;

        AssertBidirectional(repo, json, AssertReposEqual);
    }

    [TestMethod]
    public void FullyPopulatedRepo_RoundTrip()
    {
        Repo repo = new()
        {
            Id = "sdk",
            Images = [],
            McrTagsMetadataTemplate = "template.yaml",
            Name = "dotnet/sdk",
            Readmes = []
        };

        AssertRoundTrip(repo, AssertReposEqual);
    }

    [TestMethod]
    public void MinimalRepo_Bidirectional()
    {
        Repo repo = new()
        {
            Images = [],
            Name = "dotnet/aspnet"
        };

        // Null properties are omitted; only required images and name are serialized
        string json = """
            {
              "images": [],
              "name": "dotnet/aspnet"
            }
            """;

        AssertBidirectional(repo, json, AssertReposEqual);
    }

    [TestMethod]
    public void Deserialization_ImagesIsRequired_Missing()
    {
        string json = """
            {
              "name": "dotnet/runtime"
            }
            """;

        AssertDeserializationFails<Repo>(json, nameof(Repo.Images));
    }

    [TestMethod]
    public void Deserialization_ImagesIsRequired_Null()
    {
        string json = """
            {
              "images": null,
              "name": "dotnet/runtime"
            }
            """;

        AssertDeserializationFails<Repo>(json, nameof(Repo.Images));
    }

    [TestMethod]
    public void Deserialization_NameIsRequired_Missing()
    {
        string json = """
            {
              "images": []
            }
            """;

        AssertDeserializationFails<Repo>(json, nameof(Repo.Name));
    }

    [TestMethod]
    public void Deserialization_NameIsRequired_Null()
    {
        string json = """
            {
              "images": [],
              "name": null
            }
            """;

        AssertDeserializationFails<Repo>(json, nameof(Repo.Name));
    }

    private static void AssertReposEqual(Repo expected, Repo actual)
    {
        actual.Id.ShouldBe(expected.Id);
        (actual.Images?.Length ?? 0).ShouldBe(expected.Images?.Length ?? 0);
        actual.McrTagsMetadataTemplate.ShouldBe(expected.McrTagsMetadataTemplate);
        actual.Name.ShouldBe(expected.Name);
        (actual.Readmes?.Length ?? 0).ShouldBe(expected.Readmes?.Length ?? 0);
    }
}
