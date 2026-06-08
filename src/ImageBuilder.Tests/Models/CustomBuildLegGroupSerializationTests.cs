// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Serialization and deserialization tests for <see cref="CustomBuildLegGroup"/> model.
/// These tests ensure that serialization behavior does not change unexpectedly.
/// </summary>
[TestClass]
public class CustomBuildLegGroupSerializationTests
{
    [TestMethod]
    public void DefaultCustomBuildLegGroup_CannotSerialize()
    {
        // A default CustomBuildLegGroup has null Name, which violates
        // [JsonProperty(Required = Required.Always)] and cannot be serialized.
        CustomBuildLegGroup group = new();

        AssertSerializationFails(group, nameof(CustomBuildLegGroup.Name));
    }

    [TestMethod]
    public void FullyPopulatedCustomBuildLegGroup_Integral_Bidirectional()
    {
        CustomBuildLegGroup group = new()
        {
            Name = "test-scenario",
            Type = CustomBuildLegDependencyType.Integral,
            Dependencies = ["image1", "image2", "image3"]
        };

        string json = """
            {
              "name": "test-scenario",
              "type": "Integral",
              "dependencies": [
                "image1",
                "image2",
                "image3"
              ]
            }
            """;

        AssertBidirectional(group, json, AssertCustomBuildLegGroupsEqual);
    }

    [TestMethod]
    public void FullyPopulatedCustomBuildLegGroup_Supplemental_Bidirectional()
    {
        CustomBuildLegGroup group = new()
        {
            Name = "pr-build",
            Type = CustomBuildLegDependencyType.Supplemental,
            Dependencies = ["sdk-image"]
        };

        string json = """
            {
              "name": "pr-build",
              "type": "Supplemental",
              "dependencies": [
                "sdk-image"
              ]
            }
            """;

        AssertBidirectional(group, json, AssertCustomBuildLegGroupsEqual);
    }

    [TestMethod]
    public void FullyPopulatedCustomBuildLegGroup_RoundTrip()
    {
        CustomBuildLegGroup group = new()
        {
            Name = "test-scenario",
            Type = CustomBuildLegDependencyType.Supplemental,
            Dependencies = ["dep1", "dep2"]
        };

        AssertRoundTrip(group, AssertCustomBuildLegGroupsEqual);
    }

    [TestMethod]
    public void Deserialization_NameIsRequired_Missing()
    {
        string json = """
            {
              "type": "Integral",
              "dependencies": ["image1"]
            }
            """;

        AssertDeserializationFails<CustomBuildLegGroup>(json, nameof(CustomBuildLegGroup.Name));
    }

    [TestMethod]
    public void Deserialization_NameIsRequired_Null()
    {
        string json = """
            {
              "name": null,
              "type": "Integral",
              "dependencies": ["image1"]
            }
            """;

        AssertDeserializationFails<CustomBuildLegGroup>(json, nameof(CustomBuildLegGroup.Name));
    }

    [TestMethod]
    public void Deserialization_TypeIsRequired_Missing()
    {
        string json = """
            {
              "name": "test-scenario",
              "dependencies": ["image1"]
            }
            """;

        AssertDeserializationFails<CustomBuildLegGroup>(json, nameof(CustomBuildLegGroup.Type));
    }

    [TestMethod]
    public void Deserialization_DependenciesIsRequired_Missing()
    {
        string json = """
            {
              "name": "test-scenario",
              "type": "Integral"
            }
            """;

        AssertDeserializationFails<CustomBuildLegGroup>(json, nameof(CustomBuildLegGroup.Dependencies));
    }

    [TestMethod]
    public void Deserialization_DependenciesIsRequired_Null()
    {
        string json = """
            {
              "name": "test-scenario",
              "type": "Integral",
              "dependencies": null
            }
            """;

        AssertDeserializationFails<CustomBuildLegGroup>(json, nameof(CustomBuildLegGroup.Dependencies));
    }

    private static void AssertCustomBuildLegGroupsEqual(CustomBuildLegGroup expected, CustomBuildLegGroup actual)
    {
        actual.Name.ShouldBe(expected.Name);
        actual.Type.ShouldBe(expected.Type);
        actual.Dependencies.ShouldBe(expected.Dependencies);
    }
}
