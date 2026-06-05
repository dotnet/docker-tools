// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class ImageNameTests
{
    [TestMethod]
    public void Parse_SimpleImageName()
    {
        ImageName result = ImageName.Parse("ubuntu");

        result.Registry.ShouldBe("");
        result.Repo.ShouldBe("ubuntu");
        result.Tag.ShouldBe("");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_ImageWithTag()
    {
        ImageName result = ImageName.Parse("ubuntu:22.04");

        result.Registry.ShouldBe("");
        result.Repo.ShouldBe("ubuntu");
        result.Tag.ShouldBe("22.04");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_ImageWithDigest()
    {
        ImageName result = ImageName.Parse("ubuntu@sha256:abc123");

        result.Registry.ShouldBe("");
        result.Repo.ShouldBe("ubuntu");
        result.Tag.ShouldBe("");
        result.Digest.ShouldBe("sha256:abc123");
    }

    [TestMethod]
    public void Parse_ImageWithRegistry()
    {
        ImageName result = ImageName.Parse("mcr.microsoft.com/dotnet/runtime:8.0");

        result.Registry.ShouldBe("mcr.microsoft.com");
        result.Repo.ShouldBe("dotnet/runtime");
        result.Tag.ShouldBe("8.0");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_ImageWithRegistryPort()
    {
        ImageName result = ImageName.Parse("localhost:5000/myimage:latest");

        result.Registry.ShouldBe("localhost:5000");
        result.Repo.ShouldBe("myimage");
        result.Tag.ShouldBe("latest");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_ImageWithRegistryAndDigest()
    {
        ImageName result = ImageName.Parse("mcr.microsoft.com/dotnet/runtime@sha256:abc123");

        result.Registry.ShouldBe("mcr.microsoft.com");
        result.Repo.ShouldBe("dotnet/runtime");
        result.Tag.ShouldBe("");
        result.Digest.ShouldBe("sha256:abc123");
    }

    [TestMethod]
    public void Parse_ImageWithNestedRepo()
    {
        ImageName result = ImageName.Parse("myregistry.azurecr.io/team/project/image:v1");

        result.Registry.ShouldBe("myregistry.azurecr.io");
        result.Repo.ShouldBe("team/project/image");
        result.Tag.ShouldBe("v1");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_DockerHubOfficialImage_WithAutoResolve()
    {
        ImageName result = ImageName.Parse("ubuntu", autoResolveImpliedNames: true);

        result.Registry.ShouldBe(DockerHelper.DockerHubRegistry);
        result.Repo.ShouldBe("library/ubuntu");
        result.Tag.ShouldBe("");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_DockerHubUserImage_WithAutoResolve()
    {
        ImageName result = ImageName.Parse("myuser/myimage:latest", autoResolveImpliedNames: true);

        result.Registry.ShouldBe(DockerHelper.DockerHubRegistry);
        result.Repo.ShouldBe("myuser/myimage");
        result.Tag.ShouldBe("latest");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_ExplicitRegistry_WithAutoResolve()
    {
        ImageName result = ImageName.Parse("mcr.microsoft.com/dotnet/sdk:8.0", autoResolveImpliedNames: true);

        result.Registry.ShouldBe("mcr.microsoft.com");
        result.Repo.ShouldBe("dotnet/sdk");
        result.Tag.ShouldBe("8.0");
        result.Digest.ShouldBe("");
    }

    [TestMethod]
    public void Parse_SimpleRepoWithSlash_NoRegistry()
    {
        // When there's no dot or colon in the first segment, it's treated as part of the repo
        ImageName result = ImageName.Parse("myuser/myimage:tag");

        result.Registry.ShouldBe("");
        result.Repo.ShouldBe("myuser/myimage");
        result.Tag.ShouldBe("tag");
        result.Digest.ShouldBe("");
    }

    #region ToString Tests

    [TestMethod]
    public void ToString_WithRegistryAndTag()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", "8.0", null);

        imageName.ToString().ShouldBe("mcr.microsoft.com/dotnet/runtime:8.0");
    }

    [TestMethod]
    public void ToString_WithRegistryAndDigest()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", null, "sha256:abc123");

        imageName.ToString().ShouldBe("mcr.microsoft.com/dotnet/runtime@sha256:abc123");
    }

    [TestMethod]
    public void ToString_WithRegistryTagAndDigest()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", "8.0", "sha256:abc123");

        imageName.ToString().ShouldBe("mcr.microsoft.com/dotnet/runtime:8.0@sha256:abc123");
    }

    [TestMethod]
    public void ToString_RepoOnly()
    {
        var imageName = new ImageName(null, "myimage", null, null);

        imageName.ToString().ShouldBe("myimage");
    }

    [TestMethod]
    public void ToString_WithEmptyRegistry()
    {
        var imageName = new ImageName("", "myuser/myimage", "latest", null);

        imageName.ToString().ShouldBe("myuser/myimage:latest");
    }

    #endregion

    #region Implicit Conversion Tests

    [TestMethod]
    public void ImplicitConversion_StringToImageName()
    {
        ImageName imageName = "mcr.microsoft.com/dotnet/sdk:8.0";

        imageName.Registry.ShouldBe("mcr.microsoft.com");
        imageName.Repo.ShouldBe("dotnet/sdk");
        imageName.Tag.ShouldBe("8.0");
    }

    [TestMethod]
    public void ImplicitConversion_StringToImageName_ResolvesDockerHub()
    {
        // Implicit conversion uses autoResolveImpliedNames: true
        ImageName imageName = "ubuntu";

        imageName.Registry.ShouldBe(DockerHelper.DockerHubRegistry);
        imageName.Repo.ShouldBe("library/ubuntu");
    }

    [TestMethod]
    public void ImplicitConversion_ImageNameToString()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", "8.0", null);

        string result = imageName;

        result.ShouldBe("mcr.microsoft.com/dotnet/runtime:8.0");
    }

    [TestMethod]
    public void ImplicitConversion_CanPassImageNameToStringMethod()
    {
        ImageName imageName = "mcr.microsoft.com/dotnet/sdk:8.0";

        // Verifies implicit conversion works when passing to methods expecting string
        string result = AcceptString(imageName);

        result.ShouldBe("mcr.microsoft.com/dotnet/sdk:8.0");
    }

    private static string AcceptString(string value) => value;

    #endregion

    #region Round-Trip Tests

    [TestMethod]
    [DataRow("mcr.microsoft.com/dotnet/runtime:8.0")]
    [DataRow("mcr.microsoft.com/dotnet/runtime@sha256:abc123")]
    [DataRow("localhost:5000/myimage:latest")]
    [DataRow("myregistry.azurecr.io/team/project/image:v1")]
    public void RoundTrip_ParseAndToString_WithRegistry(string original)
    {
        ImageName parsed = ImageName.Parse(original);
        string result = parsed.ToString();

        result.ShouldBe(original);
    }

    [TestMethod]
    public void RoundTrip_ImplicitConversions()
    {
        string original = "mcr.microsoft.com/dotnet/sdk:8.0";

        ImageName imageName = original;
        string roundTripped = imageName;

        roundTripped.ShouldBe(original);
    }

    #endregion
}
