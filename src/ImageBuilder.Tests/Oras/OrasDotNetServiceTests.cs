// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OrasProject.Oras.Oci;
using Polly.Timeout;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests.Oras;

[TestClass]
public class OrasDotNetServiceTests
{
    public TestContext? TestContext { get; set; }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GetReferrersAsync_NullOrWhitespaceReference_ThrowsArgumentException(string? reference)
    {
        OrasDotNetService service = CreateService();

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GetReferrersAsync(reference!, TestContext?.CancellationToken ?? default));

        exception.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task PushSignatureAsync_ReadsPayloadFile()
    {
        InMemoryFileSystem fileSystem = new InMemoryFileSystem();
        OrasDotNetService service = CreateService(fileSystem);
        Descriptor subjectDescriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");

        PayloadSigningResult result = new PayloadSigningResult(
            "registry.io/repo:tag",
            subjectDescriptor,
            "/nonexistent/file.cose",
            "sha256:abcd1234");

        FileNotFoundException exception = await Should.ThrowAsync<FileNotFoundException>(async () =>
            await service.PushSignatureAsync(subjectDescriptor, result, TestContext?.CancellationToken ?? default));

        exception.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task PushSignatureAsync_ThrowsForNullResult()
    {
        OrasDotNetService service = CreateService();
        Descriptor subjectDescriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.PushSignatureAsync(subjectDescriptor, null!, TestContext?.CancellationToken ?? default));

        exception.ShouldNotBeNull();
        exception.ParamName.ShouldBe("result");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GetDescriptorAsync_NullOrWhitespaceReference_ThrowsArgumentException(string? reference)
    {
        OrasDotNetService service = CreateService();

        ArgumentException exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GetDescriptorAsync(reference!, TestContext?.CancellationToken ?? default));

        exception.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task PushSignatureAsync_NullSubjectDescriptor_ThrowsArgumentNullException()
    {
        OrasDotNetService service = CreateService();
        Descriptor descriptor = Descriptor.Create([], "application/vnd.oci.image.manifest.v1+json");
        PayloadSigningResult signedPayload = new PayloadSigningResult(
            "registry.io/repo:tag",
            descriptor,
            "/tmp/test.cose",
            "[\"thumbprint\"]");

        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.PushSignatureAsync(null!, signedPayload, TestContext?.CancellationToken ?? default));

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
            await service.GetDescriptorAsync("registry.io/repo:tag", TestContext?.CancellationToken ?? default));

        exception.ShouldBeSameAs(expectedException);
        httpClientFactory.Verify(factory => factory.CreateClient(nameof(OrasDotNetService)), Times.Once);
    }

    [TestMethod]
    public async Task GetReferrersAsync_DigestReference_DoesNotResolveSubject()
    {
        RecordingHandler handler = new();
        OrasDotNetService service = CreateService(httpClientFactory: CreateHttpClientFactory(handler));

        IReadOnlyList<ReferrerInfo> referrers = await service.GetReferrersAsync(
            $"registry.io/repo@{SubjectDigest}", TestContext?.CancellationToken ?? default);

        referrers.ShouldHaveSingleItem().ArtifactType.ShouldBe(ReferrerArtifactType);
        handler.Requests.ShouldNotContain(r => r.StartsWith("HEAD /v2/repo/manifests/"));
    }

    [TestMethod]
    public async Task GetReferrersAsync_TagReference_ResolvesSubject()
    {
        RecordingHandler handler = new();
        OrasDotNetService service = CreateService(httpClientFactory: CreateHttpClientFactory(handler));

        IReadOnlyList<ReferrerInfo> referrers = await service.GetReferrersAsync(
            "registry.io/repo:tag", TestContext?.CancellationToken ?? default);

        referrers.ShouldHaveSingleItem().ArtifactType.ShouldBe(ReferrerArtifactType);
        handler.Requests.ShouldContain("HEAD /v2/repo/manifests/tag");
    }

    [TestMethod]
    public async Task AttachArtifactAsync_PushesEmptyBlobOncePerRepository()
    {
        RecordingHandler handler = new();
        OrasDotNetService service = CreateService(httpClientFactory: CreateHttpClientFactory(handler));
        string reference = $"registry.io/repo@{SubjectDigest}";

        await service.AttachArtifactAsync(
            reference, ReferrerArtifactType, new Dictionary<string, string> { ["a"] = "1" },
            TestContext?.CancellationToken ?? default);

        await service.AttachArtifactAsync(
            reference, ReferrerArtifactType, new Dictionary<string, string> { ["a"] = "2" },
            TestContext?.CancellationToken ?? default);

        handler.Requests.Count(
            r => r.StartsWith("POST /v2/repo/blobs/uploads/", StringComparison.Ordinal)).ShouldBe(1);
        handler.Requests.Count(
            r => r.StartsWith("PUT /v2/repo/manifests/", StringComparison.Ordinal)).ShouldBe(2);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public async Task AttachArtifactAsync_RetriesManifestPushTimeout(int timeoutCount)
    {
        int failuresRemaining = timeoutCount;
        RecordingHandler handler = new()
        {
            ManifestPushFailure = () => failuresRemaining-- > 0 ? new TimeoutRejectedException() : null
        };
        OrasDotNetService service = CreateService(httpClientFactory: CreateHttpClientFactory(handler));
        Dictionary<string, string> annotations = new()
        {
            ["org.opencontainers.image.created"] = "2026-10-19T00:00:00Z",
            ["vnd.microsoft.artifact.lifecycle.end-of-life.date"] = "2026-10-19"
        };

        string digest = await service.AttachArtifactAsync(
            $"registry.io/repo@{SubjectDigest}", ReferrerArtifactType, annotations,
            TestContext?.CancellationToken ?? default);

        handler.ManifestPushContents.Count.ShouldBe(timeoutCount + 1);
        handler.ManifestPushContents.Distinct().ShouldHaveSingleItem();
        handler.Requests.Count(
            r => r.StartsWith("POST /v2/repo/blobs/uploads/", StringComparison.Ordinal)).ShouldBe(1);
        digest.ShouldBe(handler.Requests.Last(
            r => r.StartsWith("PUT /v2/repo/manifests/", StringComparison.Ordinal))
            ["PUT /v2/repo/manifests/".Length..]);
    }

    [TestMethod]
    public async Task AttachArtifactAsync_ThrowsAfterManifestPushRetriesAreExhausted()
    {
        RecordingHandler handler = new()
        {
            ManifestPushFailure = () => new TimeoutRejectedException()
        };
        OrasDotNetService service = CreateService(httpClientFactory: CreateHttpClientFactory(handler));

        await Should.ThrowAsync<TimeoutRejectedException>(() => service.AttachArtifactAsync(
            $"registry.io/repo@{SubjectDigest}", ReferrerArtifactType, new Dictionary<string, string>(),
            TestContext?.CancellationToken ?? default));

        handler.ManifestPushContents.Count.ShouldBe(3);
    }

    [TestMethod]
    public async Task AttachArtifactAsync_DoesNotRetryOtherPushFailures()
    {
        HttpRequestException expectedException = new("Registry rejected the artifact.", null, HttpStatusCode.Forbidden);
        RecordingHandler handler = new()
        {
            ManifestPushFailure = () => expectedException
        };
        OrasDotNetService service = CreateService(httpClientFactory: CreateHttpClientFactory(handler));

        HttpRequestException exception = await Should.ThrowAsync<HttpRequestException>(() => service.AttachArtifactAsync(
            $"registry.io/repo@{SubjectDigest}", ReferrerArtifactType, new Dictionary<string, string>(),
            TestContext?.CancellationToken ?? default));

        exception.ShouldBeSameAs(expectedException);
        handler.ManifestPushContents.ShouldHaveSingleItem();
    }

    [TestMethod]
    public async Task AttachArtifactAsync_DoesNotRetryAfterCancellation()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext?.CancellationToken ?? default);
        RecordingHandler handler = new()
        {
            ManifestPushFailure = () =>
            {
                cancellation.Cancel();
                return new TimeoutRejectedException();
            }
        };
        OrasDotNetService service = CreateService(httpClientFactory: CreateHttpClientFactory(handler));

        await Should.ThrowAsync<OperationCanceledException>(() => service.AttachArtifactAsync(
            $"registry.io/repo@{SubjectDigest}", ReferrerArtifactType, new Dictionary<string, string>(),
            cancellation.Token));

        handler.ManifestPushContents.ShouldHaveSingleItem();
    }

    private const string SubjectDigest = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
    private const string ReferrerArtifactType = "application/vnd.test";

    private static OrasDotNetService CreateService(
        IFileSystem? fileSystem = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        IRegistryCredentialsProvider credentialsProvider = Mock.Of<IRegistryCredentialsProvider>();
        httpClientFactory ??= CreateHttpClientFactory();
        IMemoryCache cache = Mock.Of<IMemoryCache>();
        ILogger<OrasDotNetService> logger = Mock.Of<ILogger<OrasDotNetService>>();

        return new OrasDotNetService(
            credentialsProvider,
            httpClientFactory,
            cache,
            fileSystem ?? new InMemoryFileSystem(),
            logger);
    }

    private static IHttpClientFactory CreateHttpClientFactory(HttpMessageHandler? handler = null)
    {
        Mock<IHttpClientFactory> httpClientFactory = new();
        httpClientFactory
            .Setup(factory => factory.CreateClient(nameof(OrasDotNetService)))
            .Returns(() => handler is null ? Mock.Of<HttpClient>() : new HttpClient(handler, disposeHandler: false));
        return httpClientFactory.Object;
    }

    /// <summary>
    /// A minimal fake registry for a single "repo" repository, recording each request.
    /// </summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private const string ManifestMediaType = "application/vnd.oci.image.manifest.v1+json";
        private const string IndexMediaType = "application/vnd.oci.image.index.v1+json";
        private const string UploadPath = "/v2/repo/blobs/uploads/session";

        public List<string> Requests { get; } = [];
        public List<string> ManifestPushContents { get; } = [];
        public Func<Exception?>? ManifestPushFailure { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            Requests.Add($"{request.Method} {path}");

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([])
            };

            if (request.Method == HttpMethod.Head &&
                path.StartsWith("/v2/repo/manifests/", StringComparison.Ordinal))
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(ManifestMediaType);
                response.Content.Headers.ContentLength = 123;
                response.Headers.Add("Docker-Content-Digest", SubjectDigest);
            }
            else if (request.Method == HttpMethod.Post && path == "/v2/repo/blobs/uploads/")
            {
                response.StatusCode = HttpStatusCode.Accepted;
                response.Headers.Location = new Uri(UploadPath, UriKind.Relative);
            }
            else if (request.Method == HttpMethod.Put && path == UploadPath)
            {
                response.StatusCode = HttpStatusCode.Created;
            }
            else if (request.Method == HttpMethod.Put &&
                path.StartsWith("/v2/repo/manifests/", StringComparison.Ordinal))
            {
                HttpContent content = request.Content
                    ?? throw new InvalidOperationException("A manifest push must include content.");
                ManifestPushContents.Add(await content.ReadAsStringAsync(cancellationToken));
                if (ManifestPushFailure?.Invoke() is Exception exception)
                {
                    throw exception;
                }

                response.StatusCode = HttpStatusCode.Created;
                response.Headers.Add("Docker-Content-Digest", path["/v2/repo/manifests/".Length..]);

                // Tells ORAS the registry supports the referrers API, so it doesn't fall back to updating a
                // referrers tag index.
                response.Headers.Add("OCI-Subject", SubjectDigest);
            }
            else if (request.Method == HttpMethod.Get && path == $"/v2/repo/referrers/{SubjectDigest}")
            {
                string referrersIndex = $$"""
                    {
                      "schemaVersion": 2,
                      "mediaType": "{{IndexMediaType}}",
                      "manifests": [
                        {
                          "mediaType": "{{ManifestMediaType}}",
                          "digest": "sha256:2222222222222222222222222222222222222222222222222222222222222222",
                          "size": 456,
                          "artifactType": "{{ReferrerArtifactType}}"
                        }
                      ]
                    }
                    """;
                response.Content = new StringContent(referrersIndex, Encoding.UTF8, IndexMediaType);
            }
            else
            {
                response.StatusCode = HttpStatusCode.InternalServerError;
            }

            return response;
        }
    }
}
