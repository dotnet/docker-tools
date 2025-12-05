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
}
