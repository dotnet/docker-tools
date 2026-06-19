// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using static Microsoft.DotNet.ImageBuilder.Tests.Models.SerializationHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models;

/// <summary>
/// Schema contract tests for <see cref="ImageInfoArtifact"/> model.
/// </summary>
[TestClass]
public class ImageInfoArtifactSerializationTests
{
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
}
