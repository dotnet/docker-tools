// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="Repo"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
public class RepoSerializationTests
{
    [Fact]
    public void DefaultRepo_CannotSerialize()
    {
        // A default Repo has null Images and Name, which violate
        // [JsonProperty(Required = Required.Always)] and cannot be serialized.
        Repo repo = new();

        AssertSerializationFails(repo, nameof(Repo.Images));
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Deserialization_ImagesIsRequired_Missing()
    {
        string json = """
            {
              "name": "dotnet/runtime"
            }
            """;

        AssertDeserializationFails<Repo>(json, nameof(Repo.Images));
    }

    [Fact]
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

    [Fact]
    public void Deserialization_NameIsRequired_Missing()
    {
        string json = """
            {
              "images": []
            }
            """;

        AssertDeserializationFails<Repo>(json, nameof(Repo.Name));
    }

    [Fact]
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
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Images?.Length ?? 0, actual.Images?.Length ?? 0);
        Assert.Equal(expected.McrTagsMetadataTemplate, actual.McrTagsMetadataTemplate);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Readmes?.Length ?? 0, actual.Readmes?.Length ?? 0);
    }
}
