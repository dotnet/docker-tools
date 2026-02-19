// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Microsoft.Extensions.Logging;
using OrasProject.Oras.Oci;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Oras;

public class OrasDotNetServiceTests
{
    [Fact]
    public async Task PushSignatureAsync_ReadsPayloadFile()
    {
        using var tempFolder = TestHelper.UseTempFolder();
        var nonExistentFile = Path.Combine(tempFolder.Path, "nonexistent.cose");
        var fileInfo = new FileInfo(nonExistentFile);

        var service = CreateService();
        var subjectDescriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");

        var result = new PayloadSigningResult(
            "registry.io/repo:tag",
            subjectDescriptor,
            fileInfo,
            "sha256:abcd1234");

        var exception = await Should.ThrowAsync<FileNotFoundException>(async () =>
            await service.PushSignatureAsync(subjectDescriptor, result));

        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task PushSignatureAsync_ThrowsForNullResult()
    {
        var service = CreateService();
        var subjectDescriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.PushSignatureAsync(subjectDescriptor, null!));

        exception.ShouldNotBeNull();
        exception.ParamName.ShouldBe("result");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetDescriptorAsync_NullOrWhitespaceReference_ThrowsArgumentException(string? reference)
    {
        var service = CreateService();

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GetDescriptorAsync(reference!));

        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task PushSignatureAsync_NullSubjectDescriptor_ThrowsArgumentNullException()
    {
        var service = CreateService();
        var descriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");
        var signedPayload = new PayloadSigningResult(
            "registry.io/repo:tag",
            descriptor,
            new FileInfo("/tmp/test.cose"),
            "[\"thumbprint\"]");

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.PushSignatureAsync(null!, signedPayload));

        exception.ShouldNotBeNull();
        exception.ParamName.ShouldBe("subjectDescriptor");
    }

    private static OrasDotNetService CreateService()
    {
        var credentialsProvider = Mock.Of<IRegistryCredentialsProvider>();
        var httpClientProvider = new Mock<IHttpClientProvider>();
        httpClientProvider
            .Setup(p => p.GetClient())
            .Returns(new HttpClient());
        var cache = Mock.Of<IMemoryCache>();
        var logger = Mock.Of<ILogger<OrasDotNetService>>();

        return new OrasDotNetService(
            credentialsProvider,
            httpClientProvider.Object,
            cache,
            logger);
    }
}
