// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Logging;
using Moq;
using OrasProject.Oras.Registry.Remote.Exceptions;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class LifecycleMetadataServiceTests
{
    private const string Digest = "myregistry.azurecr.io/public/dotnet/runtime@sha256:0123456789abcdef";

    /// <summary>
    /// A transient registry failure (e.g. HTTP 429) must be surfaced, not silently treated as
    /// "no annotation exists". Swallowing it produces a false negative that lets already-annotated
    /// digests be re-annotated with a conflicting EOL date.
    /// </summary>
    [Fact]
    public async Task IsDigestAnnotatedForEolAsync_DoesNotSwallowRegistryErrors()
    {
        ResponseException rateLimitException = CreateRateLimitException();

        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.GetReferrersAsync(Digest, false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(rateLimitException);

        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        ResponseException thrown = await Should.ThrowAsync<ResponseException>(
            () => service.IsDigestAnnotatedForEolAsync(Digest));
        thrown.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task IsDigestAnnotatedForEolAsync_NoReferrers_ReturnsNull()
    {
        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.GetReferrersAsync(Digest, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        Manifest? result = await service.IsDigestAnnotatedForEolAsync(Digest);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task IsDigestAnnotatedForEolAsync_ExistingLifecycleReferrer_ReturnsManifest()
    {
        ReferrerInfo lifecycleReferrer = new(
            Digest: "myregistry.azurecr.io/public/dotnet/runtime@sha256:annotationdigest",
            ArtifactType: OciArtifactType.Lifecycle)
        {
            Annotations = new Dictionary<string, string>
            {
                [LifecycleMetadataService.EndOfLifeAnnotation] = "2026-05-22"
            }
        };

        Mock<IOrasService> orasServiceMock = new();
        orasServiceMock
            .Setup(o => o.GetReferrersAsync(Digest, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lifecycleReferrer]);

        LifecycleMetadataService service = CreateService(orasServiceMock.Object);

        Manifest? result = await service.IsDigestAnnotatedForEolAsync(Digest);

        result.ShouldNotBeNull();
        result.Annotations[LifecycleMetadataService.EndOfLifeAnnotation].ShouldBe("2026-05-22");
    }

    private static LifecycleMetadataService CreateService(IOrasService orasService) =>
        new(orasService, Mock.Of<ILogger<LifecycleMetadataService>>());

    private static ResponseException CreateRateLimitException()
    {
        HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
        return new ResponseException(
            response,
            responseBody: "TOOMANYREQUESTS: exceeded the per-identity rate limit of 250 requests in a 60 second window.");
    }
}
