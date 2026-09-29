// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Logging;
using Moq;
using OrasProject.Oras.Registry.Remote.Exceptions;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests;

[TestClass]
public class LifecycleMetadataServiceTests
{
    public TestContext? TestContext { get; set; }

    private const string Registry = "myregistry.azurecr.io";
    private const string Repository = "public/dotnet/runtime";
    private const string Digest = $"{Registry}/{Repository}@sha256:0123456789abcdef";

    /// <summary>
    /// A transient registry failure (e.g. HTTP 429) must be surfaced, not silently treated as
    /// "no annotation exists". Swallowing it produces a false negative that lets already-annotated
    /// digests be re-annotated with a conflicting EOL date.
    /// </summary>
    [TestMethod]
    public async Task GetLatestLifecycleArtifactAsync_DoesNotSwallowRegistryErrors()
    {
        ResponseException rateLimitException = CreateRateLimitException();

        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.GetReferrersAsync(Digest, It.IsAny<CancellationToken>(), false))
            .ThrowsAsync(rateLimitException);

        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        ResponseException thrown = await Should.ThrowAsync<ResponseException>(
            () => service.GetLatestLifecycleArtifactAsync(Digest, includeInternal: true, CancellationToken));
        thrown.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [TestMethod]
    public async Task GetLatestLifecycleArtifactAsync_NoLifecycleReferrers_ReturnsNull()
    {
        LifecycleMetadataService service = CreateService(
            CreateReferrer("signature", OciArtifactType.NotarySignatureV2, created: "2026-01-01T00:00:00Z"));

        LifecycleArtifact? result =
            await service.GetLatestLifecycleArtifactAsync(Digest, includeInternal: true, CancellationToken);

        result.ShouldBeNull();
    }

    [TestMethod]
    public async Task GetLatestLifecycleArtifactAsync_ParsesLifecycleReferrer()
    {
        LifecycleMetadataService service = CreateService(
            CreateReferrer("lifecycle", created: "2026-05-01T12:34:56.1234567Z", endOfLife: "2026-05-22"));

        LifecycleArtifact? result =
            await service.GetLatestLifecycleArtifactAsync(Digest, includeInternal: false, CancellationToken);

        result.ShouldNotBeNull();
        result.Referrer.Digest.ShouldBe($"{Registry}/{Repository}@sha256:lifecycle");
        result.Referrer.Created.ShouldBe(new DateTimeOffset(2026, 5, 1, 12, 34, 56, TimeSpan.Zero).AddTicks(1234567));
        result.Referrer.IsInternal.ShouldBeFalse();
        result.EndOfLifeDate.ShouldBe(new DateOnly(2026, 5, 22));
    }

    [TestMethod]
    public async Task GetLatestLifecycleArtifactAsync_MultipleLifecycleReferrers_ReturnsMostRecentlyCreated()
    {
        LifecycleMetadataService service = CreateService(
            CreateReferrer("noCreated", endOfLife: "2026-01-01"),
            CreateReferrer("older", created: "2026-01-01T00:00:00Z", endOfLife: "2026-01-02"),
            CreateReferrer("newest", created: "2026-03-01T00:00:00Z", endOfLife: "2026-01-03"),
            CreateReferrer("newer", created: "2026-02-01T00:00:00Z", endOfLife: "2026-01-04"));

        LifecycleArtifact? result =
            await service.GetLatestLifecycleArtifactAsync(Digest, includeInternal: false, CancellationToken);

        result.ShouldNotBeNull();
        result.Referrer.Digest.ShouldBe($"{Registry}/{Repository}@sha256:newest");
    }

    [TestMethod]
    public async Task GetLatestLifecycleArtifactAsync_NoCreatedTimestamps_ReturnsFirstReferrer()
    {
        LifecycleMetadataService service = CreateService(
            CreateReferrer("first", endOfLife: "2026-01-01"),
            CreateReferrer("second", created: "not-a-date", endOfLife: "2026-01-02"));

        LifecycleArtifact? result =
            await service.GetLatestLifecycleArtifactAsync(Digest, includeInternal: false, CancellationToken);

        result.ShouldNotBeNull();
        result.Referrer.Digest.ShouldBe($"{Registry}/{Repository}@sha256:first");
    }

    [TestMethod]
    [DataRow(false, "public")]
    [DataRow(true, "internal")]
    public async Task GetLatestLifecycleArtifactAsync_FiltersInternalArtifacts(bool includeInternal, string expectedDigest)
    {
        LifecycleMetadataService service = CreateService(
            CreateReferrer("public", created: "2026-01-01T00:00:00Z", endOfLife: "2026-01-01"),
            CreateReferrer("internal", created: "2026-02-01T00:00:00Z", endOfLife: "2026-01-01", markAsInternal: true));

        LifecycleArtifact? result =
            await service.GetLatestLifecycleArtifactAsync(Digest, includeInternal, CancellationToken);

        result.ShouldNotBeNull();
        result.Referrer.Digest.ShouldBe($"{Registry}/{Repository}@sha256:{expectedDigest}");
    }

    [TestMethod]
    public async Task GetLatestLifecycleArtifactAsync_OnlyInternalArtifacts_ExcludingInternal_ReturnsNull()
    {
        LifecycleMetadataService service = CreateService(
            CreateReferrer("internal", created: "2026-01-01T00:00:00Z", endOfLife: "2026-01-01", markAsInternal: true));

        LifecycleArtifact? result =
            await service.GetLatestLifecycleArtifactAsync(Digest, includeInternal: false, CancellationToken);

        result.ShouldBeNull();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task AnnotateEolDigestAsync_AttachesLifecycleArtifact(bool markAsInternal)
    {
        IDictionary<string, string>? attachedAnnotations = null;
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.AttachArtifactAsync(
                Digest, OciArtifactType.Lifecycle, It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, IDictionary<string, string>, CancellationToken>(
                (_, _, annotations, _) => attachedAnnotations = annotations)
            .ReturnsAsync("sha256:lifecycle");

        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        DateTimeOffset before = DateTimeOffset.UtcNow;
        LifecycleArtifact? result = await service.AnnotateEolDigestAsync(
            Digest, new DateOnly(2026, 5, 22), markAsInternal, CancellationToken);
        DateTimeOffset after = DateTimeOffset.UtcNow;

        result.ShouldNotBeNull();
        result.Referrer.Digest.ShouldBe($"{Registry}/{Repository}@sha256:lifecycle");
        result.Referrer.ArtifactType.ShouldBe(OciArtifactType.Lifecycle);
        result.Referrer.IsInternal.ShouldBe(markAsInternal);
        DateTimeOffset? created = result.Referrer.Created;
        created.ShouldNotBeNull();
        created.Value.ShouldBeInRange(before, after);
        result.EndOfLifeDate.ShouldBe(new DateOnly(2026, 5, 22));

        // The returned artifact must describe exactly what was pushed.
        result.Referrer.Annotations.ShouldBe(attachedAnnotations);
    }

    [TestMethod]
    public async Task AnnotateEolDigestAsync_AttachFails_ReturnsNull()
    {
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.AttachArtifactAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateRateLimitException());

        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        LifecycleArtifact? result = await service.AnnotateEolDigestAsync(
            Digest, new DateOnly(2026, 5, 22), markAsInternal: false, CancellationToken);

        result.ShouldBeNull();
    }

    private CancellationToken CancellationToken => TestContext?.CancellationToken ?? default;

    [TestMethod]
    [DataRow("2026-01-01", true, LifecycleMetadataAttachmentStatus.AlreadyMatching)]
    [DataRow("2026-01-01", false, LifecycleMetadataAttachmentStatus.AlreadyMatching)]
    [DataRow("2025-01-01", false, LifecycleMetadataAttachmentStatus.ConflictSkipped)]
    [DataRow(null, false, LifecycleMetadataAttachmentStatus.ConflictSkipped)]
    public async Task AttachLifecycleMetadataAsync_ExistingMetadata_ReturnsSkippedResult(
        string? existingDate, bool stopOnConflict, LifecycleMetadataAttachmentStatus expected)
    {
        ReferrerInfo referrer = CreateReferrer("existing", endOfLife: existingDate);
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock.Setup(o => o.GetReferrersAsync(Digest, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([referrer]);
        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        LifecycleMetadataAttachmentResult result = await service.AttachLifecycleMetadataAsync(
            Digest, new DateOnly(2026, 1, 1), markAsInternal: false, stopOnConflict, CancellationToken);

        result.Status.ShouldBe(expected);
        result.Artifact.Referrer.ShouldBe(referrer);
        orasServiceMock.Verify(o => o.AttachArtifactAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [TestMethod]
    [DataRow("2025-01-01")]
    [DataRow(null)]
    public async Task AttachLifecycleMetadataAsync_ConflictingDate_Throws(string? existingDate)
    {
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock.Setup(o => o.GetReferrersAsync(Digest, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([CreateReferrer("existing", endOfLife: existingDate)]);
        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => service.AttachLifecycleMetadataAsync(
                Digest, new DateOnly(2026, 1, 1), markAsInternal: false, stopOnConflict: true, CancellationToken));

        exception.Message.ShouldContain(Digest);
        exception.Message.ShouldContain("conflicts");
        orasServiceMock.Verify(o => o.AttachArtifactAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    [TestMethod]
    [DataRow(false, false, LifecycleMetadataAttachmentStatus.Attached)]
    [DataRow(false, true, LifecycleMetadataAttachmentStatus.Attached)]
    [DataRow(true, false, LifecycleMetadataAttachmentStatus.Attached)]
    [DataRow(true, true, LifecycleMetadataAttachmentStatus.AlreadyMatching)]
    public async Task AttachLifecycleMetadataAsync_RespectsInternalVisibility(
        bool markAsInternal, bool existingInternal, LifecycleMetadataAttachmentStatus expected)
    {
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock.Setup(o => o.GetReferrersAsync(Digest, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(existingInternal
                ? [CreateReferrer("internal", endOfLife: "2026-01-01", markAsInternal: true)]
                : []);
        orasServiceMock.Setup(o => o.AttachArtifactAsync(
            Digest, OciArtifactType.Lifecycle, It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("sha256:new");
        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        LifecycleMetadataAttachmentResult result = await service.AttachLifecycleMetadataAsync(
            Digest, new DateOnly(2026, 1, 1), markAsInternal, stopOnConflict: true, CancellationToken);

        result.Status.ShouldBe(expected);
        result.Artifact.Referrer.IsInternal.ShouldBe(markAsInternal);
        orasServiceMock.Verify(o => o.AttachArtifactAsync(
            Digest, OciArtifactType.Lifecycle, It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            expected == LifecycleMetadataAttachmentStatus.Attached ? Times.Once() : Times.Never());
    }

    [TestMethod]
    public async Task AttachLifecycleMetadataAsync_AttachFails_Throws()
    {
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock.Setup(o => o.GetReferrersAsync(Digest, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync([]);
        orasServiceMock.Setup(o => o.AttachArtifactAsync(
            Digest, OciArtifactType.Lifecycle, It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateRateLimitException());
        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        await Should.ThrowAsync<InvalidOperationException>(() => service.AttachLifecycleMetadataAsync(
            Digest, new DateOnly(2026, 1, 1), markAsInternal: false, stopOnConflict: true, CancellationToken));
    }

    [TestMethod]
    public async Task AttachLifecycleMetadataAsync_LookupFails_DoesNotAttach()
    {
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock.Setup(o => o.GetReferrersAsync(Digest, It.IsAny<CancellationToken>(), false))
            .ThrowsAsync(CreateRateLimitException());
        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        await Should.ThrowAsync<ResponseException>(() => service.AttachLifecycleMetadataAsync(
            Digest, new DateOnly(2026, 1, 1), markAsInternal: false, stopOnConflict: false, CancellationToken));

        orasServiceMock.Verify(o => o.AttachArtifactAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never());
    }

    private static ReferrerInfo CreateReferrer(
        string digest,
        string artifactType = OciArtifactType.Lifecycle,
        string? created = null,
        string? endOfLife = null,
        bool markAsInternal = false)
    {
        Dictionary<string, string> annotations = [];
        if (created is not null)
        {
            annotations[OciAnnotations.ImageCreated] = created;
        }

        if (endOfLife is not null)
        {
            annotations[LifecycleAnnotations.EndOfLife] = endOfLife;
        }

        if (markAsInternal)
        {
            annotations[ImageBuilderAnnotations.Internal] = "true";
        }

        return new ReferrerInfo($"{Registry}/{Repository}@sha256:{digest}", artifactType)
        {
            Annotations = annotations
        };
    }

    private static LifecycleMetadataService CreateService(params ReferrerInfo[] referrers)
    {
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.GetReferrersAsync(Digest, It.IsAny<CancellationToken>(), false))
            .ReturnsAsync(referrers);

        return CreateService(orasServiceMock.Object);
    }

    private static LifecycleMetadataService CreateService(IOrasService orasService) =>
        new(orasService, Mock.Of<ILogger<LifecycleMetadataService>>());

    private static ResponseException CreateRateLimitException()
    {
        using HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
        return new ResponseException(
            response,
            responseBody: "TOOMANYREQUESTS: exceeded the per-identity rate limit of 250 requests in a 60 second window.");
    }
}
