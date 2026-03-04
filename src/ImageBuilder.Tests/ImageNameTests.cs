// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class ImageNameTests
{
    [Fact]
    public void Parse_SimpleImageName()
    {
        ImageName result = ImageName.Parse("ubuntu");

        Assert.Equal("", result.Registry);
        Assert.Equal("ubuntu", result.Repo);
        Assert.Equal("", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_ImageWithTag()
    {
        ImageName result = ImageName.Parse("ubuntu:22.04");

        Assert.Equal("", result.Registry);
        Assert.Equal("ubuntu", result.Repo);
        Assert.Equal("22.04", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_ImageWithDigest()
    {
        ImageName result = ImageName.Parse("ubuntu@sha256:abc123");

        Assert.Equal("", result.Registry);
        Assert.Equal("ubuntu", result.Repo);
        Assert.Equal("", result.Tag);
        Assert.Equal("sha256:abc123", result.Digest);
    }

    [Fact]
    public void Parse_ImageWithRegistry()
    {
        ImageName result = ImageName.Parse("mcr.microsoft.com/dotnet/runtime:8.0");

        Assert.Equal("mcr.microsoft.com", result.Registry);
        Assert.Equal("dotnet/runtime", result.Repo);
        Assert.Equal("8.0", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_ImageWithRegistryPort()
    {
        ImageName result = ImageName.Parse("localhost:5000/myimage:latest");

        Assert.Equal("localhost:5000", result.Registry);
        Assert.Equal("myimage", result.Repo);
        Assert.Equal("latest", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_ImageWithRegistryAndDigest()
    {
        ImageName result = ImageName.Parse("mcr.microsoft.com/dotnet/runtime@sha256:abc123");

        Assert.Equal("mcr.microsoft.com", result.Registry);
        Assert.Equal("dotnet/runtime", result.Repo);
        Assert.Equal("", result.Tag);
        Assert.Equal("sha256:abc123", result.Digest);
    }

    [Fact]
    public void Parse_ImageWithNestedRepo()
    {
        ImageName result = ImageName.Parse("myregistry.azurecr.io/team/project/image:v1");

        Assert.Equal("myregistry.azurecr.io", result.Registry);
        Assert.Equal("team/project/image", result.Repo);
        Assert.Equal("v1", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_DockerHubOfficialImage_WithAutoResolve()
    {
        ImageName result = ImageName.Parse("ubuntu", autoResolveImpliedNames: true);

        Assert.Equal(DockerHelper.DockerHubRegistry, result.Registry);
        Assert.Equal("library/ubuntu", result.Repo);
        Assert.Equal("", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_DockerHubUserImage_WithAutoResolve()
    {
        ImageName result = ImageName.Parse("myuser/myimage:latest", autoResolveImpliedNames: true);

        Assert.Equal(DockerHelper.DockerHubRegistry, result.Registry);
        Assert.Equal("myuser/myimage", result.Repo);
        Assert.Equal("latest", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_ExplicitRegistry_WithAutoResolve()
    {
        ImageName result = ImageName.Parse("mcr.microsoft.com/dotnet/sdk:8.0", autoResolveImpliedNames: true);

        Assert.Equal("mcr.microsoft.com", result.Registry);
        Assert.Equal("dotnet/sdk", result.Repo);
        Assert.Equal("8.0", result.Tag);
        Assert.Equal("", result.Digest);
    }

    [Fact]
    public void Parse_SimpleRepoWithSlash_NoRegistry()
    {
        // When there's no dot or colon in the first segment, it's treated as part of the repo
        ImageName result = ImageName.Parse("myuser/myimage:tag");

        Assert.Equal("", result.Registry);
        Assert.Equal("myuser/myimage", result.Repo);
        Assert.Equal("tag", result.Tag);
        Assert.Equal("", result.Digest);
    }

    #region ToString Tests

    [Fact]
    public void ToString_WithRegistryAndTag()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", "8.0", null);

        Assert.Equal("mcr.microsoft.com/dotnet/runtime:8.0", imageName.ToString());
    }

    [Fact]
    public void ToString_WithRegistryAndDigest()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", null, "sha256:abc123");

        Assert.Equal("mcr.microsoft.com/dotnet/runtime@sha256:abc123", imageName.ToString());
    }

    [Fact]
    public void ToString_WithRegistryTagAndDigest()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", "8.0", "sha256:abc123");

        Assert.Equal("mcr.microsoft.com/dotnet/runtime:8.0@sha256:abc123", imageName.ToString());
    }

    [Fact]
    public void ToString_RepoOnly()
    {
        var imageName = new ImageName(null, "myimage", null, null);

        Assert.Equal("myimage", imageName.ToString());
    }

    [Fact]
    public void ToString_WithEmptyRegistry()
    {
        var imageName = new ImageName("", "myuser/myimage", "latest", null);

        Assert.Equal("myuser/myimage:latest", imageName.ToString());
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversion_StringToImageName()
    {
        ImageName imageName = "mcr.microsoft.com/dotnet/sdk:8.0";

        Assert.Equal("mcr.microsoft.com", imageName.Registry);
        Assert.Equal("dotnet/sdk", imageName.Repo);
        Assert.Equal("8.0", imageName.Tag);
    }

    [Fact]
    public void ImplicitConversion_StringToImageName_ResolvesDockerHub()
    {
        // Implicit conversion uses autoResolveImpliedNames: true
        ImageName imageName = "ubuntu";

        Assert.Equal(DockerHelper.DockerHubRegistry, imageName.Registry);
        Assert.Equal("library/ubuntu", imageName.Repo);
    }

    [Fact]
    public void ImplicitConversion_ImageNameToString()
    {
        var imageName = new ImageName("mcr.microsoft.com", "dotnet/runtime", "8.0", null);

        string result = imageName;

        Assert.Equal("mcr.microsoft.com/dotnet/runtime:8.0", result);
    }

    [Fact]
    public void ImplicitConversion_CanPassImageNameToStringMethod()
    {
        ImageName imageName = "mcr.microsoft.com/dotnet/sdk:8.0";

        // Verifies implicit conversion works when passing to methods expecting string
        string result = AcceptString(imageName);

        Assert.Equal("mcr.microsoft.com/dotnet/sdk:8.0", result);
    }

    private static string AcceptString(string value) => value;

    #endregion

    #region Round-Trip Tests

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/runtime:8.0")]
    [InlineData("mcr.microsoft.com/dotnet/runtime@sha256:abc123")]
    [InlineData("localhost:5000/myimage:latest")]
    [InlineData("myregistry.azurecr.io/team/project/image:v1")]
    public void RoundTrip_ParseAndToString_WithRegistry(string original)
    {
        ImageName parsed = ImageName.Parse(original);
        string result = parsed.ToString();

        Assert.Equal(original, result);
    }

    [Fact]
    public void RoundTrip_ImplicitConversions()
    {
        string original = "mcr.microsoft.com/dotnet/sdk:8.0";

        ImageName imageName = original;
        string roundTripped = imageName;

        Assert.Equal(original, roundTripped);
    }

    #endregion
}
