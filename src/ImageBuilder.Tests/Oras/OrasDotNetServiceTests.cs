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

namespace Microsoft.DotNet.ImageBuilder.Tests.Oras;

[TestClass]
public class OrasDotNetServiceTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GetReferrersAsync_NullOrWhitespaceReference_ThrowsArgumentException(string? reference)
    {
        var service = CreateService();

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GetReferrersAsync(reference!));

        exception.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task PushSignatureAsync_ReadsPayloadFile()
    {
        var fileSystem = new InMemoryFileSystem();
        var service = CreateService(fileSystem);
        var subjectDescriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");

        var result = new PayloadSigningResult(
            "registry.io/repo:tag",
            subjectDescriptor,
            "/nonexistent/file.cose",
            "sha256:abcd1234");

        var exception = await Should.ThrowAsync<FileNotFoundException>(async () =>
            await service.PushSignatureAsync(subjectDescriptor, result));

        exception.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task PushSignatureAsync_ThrowsForNullResult()
    {
        var service = CreateService();
        var subjectDescriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.PushSignatureAsync(subjectDescriptor, null!));

        exception.ShouldNotBeNull();
        exception.ParamName.ShouldBe("result");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GetDescriptorAsync_NullOrWhitespaceReference_ThrowsArgumentException(string? reference)
    {
        var service = CreateService();

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GetDescriptorAsync(reference!));

        exception.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task PushSignatureAsync_NullSubjectDescriptor_ThrowsArgumentNullException()
    {
        var service = CreateService();
        var descriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");
        var signedPayload = new PayloadSigningResult(
            "registry.io/repo:tag",
            descriptor,
            "/tmp/test.cose",
            "[\"thumbprint\"]");

        var exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.PushSignatureAsync(null!, signedPayload));

        exception.ShouldNotBeNull();
        exception.ParamName.ShouldBe("subjectDescriptor");
    }

    [TestMethod]
    public async Task GetDescriptorAsync_UsesOrasNamedHttpClient()
    {
        Mock<IHttpClientFactory> httpClientFactory = new(MockBehavior.Strict);
        InvalidOperationException expectedException = new("Expected named client.");
        httpClientFactory
            .Setup(factory => factory.CreateClient(nameof(OrasDotNetService)))
            .Throws(expectedException);

        OrasDotNetService service = CreateService(httpClientFactory: httpClientFactory.Object);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.GetDescriptorAsync("registry.io/repo:tag"));

        exception.ShouldBeSameAs(expectedException);
        httpClientFactory.Verify(factory => factory.CreateClient(nameof(OrasDotNetService)), Times.Once);
    }

    private static OrasDotNetService CreateService(
        IFileSystem? fileSystem = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        var credentialsProvider = Mock.Of<IRegistryCredentialsProvider>();
        httpClientFactory ??= CreateHttpClientFactory();
        var cache = Mock.Of<IMemoryCache>();
        var logger = Mock.Of<ILogger<OrasDotNetService>>();

        return new OrasDotNetService(
            credentialsProvider,
            httpClientFactory,
            cache,
            fileSystem ?? new InMemoryFileSystem(),
            logger);
    }

    private static IHttpClientFactory CreateHttpClientFactory()
    {
        Mock<IHttpClientFactory> httpClientFactory = new();
        httpClientFactory
            .Setup(factory => factory.CreateClient(nameof(OrasDotNetService)))
            .Returns(Mock.Of<HttpClient>());
        return httpClientFactory.Object;
    }
}
