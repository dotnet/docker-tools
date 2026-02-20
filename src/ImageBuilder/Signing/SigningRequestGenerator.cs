// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Notary;
using Microsoft.DotNet.ImageBuilder.Oras;
using OrasDescriptor = OrasProject.Oras.Oci.Descriptor;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Generates signing requests from image artifact details by fetching OCI descriptors from the registry.
/// </summary>
public class SigningRequestGenerator : ISigningRequestGenerator
{
    private readonly IOrasDescriptorService _descriptorService;
    private readonly ILogger<SigningRequestGenerator> _logger;

    public SigningRequestGenerator(
        IOrasDescriptorService descriptorService,
        ILogger<SigningRequestGenerator> logger)
    {
        _descriptorService = descriptorService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ImageSigningRequest>> GenerateSigningRequestsAsync(
        ImageArtifactDetails imageArtifactDetails,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> imageDigests = ExtractAllImageDigests(imageArtifactDetails);

        _logger.LogInformation("Generating signing requests for {Count} images.", imageDigests.Count);

        var requests = new List<ImageSigningRequest>();

        foreach (string imageDigest in imageDigests)
        {
            OrasDescriptor descriptor = await _descriptorService.GetDescriptorAsync(imageDigest, cancellationToken);
            ImageSigningRequest request = ConstructSigningRequest(imageDigest, descriptor);
            requests.Add(request);
        }

        return requests;
    }

    /// <summary>
    /// Extracts all digest references (platform manifests and manifest lists) from the artifact details.
    /// </summary>
    private static IReadOnlyList<string> ExtractAllImageDigests(ImageArtifactDetails imageArtifactDetails)
    {
        IEnumerable<string> platformDigests = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .SelectMany(image => image.Platforms
                    .Where(platform => !string.IsNullOrEmpty(platform.Digest))
                    .Select(platform => platform.Digest)));

        IEnumerable<string> manifestListDigests = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .Where(image => image.Manifest is not null && !string.IsNullOrEmpty(image.Manifest.Digest))
                .Select(image => image.Manifest.Digest));

        return [..platformDigests, ..manifestListDigests];
    }

    /// <summary>
    /// Builds an <see cref="ImageSigningRequest"/> from a reference and its resolved OCI descriptor.
    /// </summary>
    private static ImageSigningRequest ConstructSigningRequest(string imageDigest, OrasDescriptor descriptor)
    {
        var ociDescriptor = new Models.Oci.Descriptor(
            MediaType: descriptor.MediaType,
            Digest: descriptor.Digest,
            Size: descriptor.Size);

        var payload = new Payload(TargetArtifact: ociDescriptor);

        var request = new ImageSigningRequest(
            ImageName: imageDigest,
            Descriptor: descriptor,
            Payload: payload);

        return request;
    }
}
