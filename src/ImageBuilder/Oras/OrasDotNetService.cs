// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrasProject.Oras;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// ORAS .NET library implementation for resolving OCI descriptors and pushing Notary v2 signatures.
/// </summary>
public class OrasDotNetService(
    IRegistryCredentialsProvider credentialsProvider,
    IHttpClientProvider httpClientProvider,
    IMemoryCache cache,
    ILogger<OrasDotNetService> logger,
    IRegistryCredentialsHost? credentialsHost = null)
        : IOrasDescriptorService, IOrasSignatureService
{
    /// <summary>
    /// OCI artifact type for Notary v2 signatures.
    /// </summary>
    private const string NotarySignatureArtifactType = "application/vnd.cncf.notary.signature";

    /// <summary>
    /// Media type for COSE signature envelopes per the Notary v2 spec.
    /// </summary>
    private const string CoseMediaType = "application/cose";

    /// <summary>
    /// Annotation key for the certificate chain thumbprints per the Notary v2 spec.
    /// </summary>
    private const string CertificateChainAnnotation = "io.cncf.notary.x509chain.thumbprint#S256";

    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<OrasDotNetService> _logger = logger;
    private readonly OrasCredentialProviderAdapter _credentialProvider = new(credentialsProvider, credentialsHost);

    /// <inheritdoc/>
    public async Task<Descriptor> GetDescriptorAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        _logger.LogInformation("Resolving descriptor for reference: {Reference}", reference);

        Repository repository = CreateRepository(reference);
        Descriptor descriptor = await repository.ResolveAsync(reference, cancellationToken);

        _logger.LogInformation(
            "Resolved descriptor: mediaType={MediaType}, digest={Digest}, size={Size}",
            descriptor.MediaType, descriptor.Digest, descriptor.Size);

        return descriptor;
    }

    /// <inheritdoc/>
    public async Task<string> PushSignatureAsync(
        Descriptor subjectDescriptor,
        PayloadSigningResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subjectDescriptor);
        ArgumentNullException.ThrowIfNull(result);

        Repository repository = CreateRepository(result.ImageName);

        byte[] payloadBytes = await File.ReadAllBytesAsync(result.SignedPayloadFilePath, cancellationToken);
        Descriptor signatureLayerDescriptor = Descriptor.Create(payloadBytes, CoseMediaType);

        using MemoryStream payloadStream = new(payloadBytes);
        await repository.PushAsync(signatureLayerDescriptor, payloadStream, cancellationToken);

        Dictionary<string, string> annotations = new()
        {
            [CertificateChainAnnotation] = result.CertificateChain
        };

        PackManifestOptions options = new()
        {
            ManifestAnnotations = annotations,
            Subject = subjectDescriptor,
            Layers = [signatureLayerDescriptor]
        };

        _logger.LogInformation("Pushing signature for {ImageName}", result.ImageName);

        Descriptor signatureDescriptor =
            await Packer.PackManifestAsync(
                pusher: repository,
                version: Packer.ManifestVersion.Version1_1,
                artifactType: NotarySignatureArtifactType,
                options: options,
                cancellationToken);

        _logger.LogInformation("Signature pushed: {Digest}", signatureDescriptor.Digest);

        return signatureDescriptor.Digest;
    }

    /// <summary>
    /// Creates an authenticated ORAS repository client for the given reference.
    /// </summary>
    /// <param name="reference">Full registry reference (e.g., "registry.io/repo:tag").</param>
    private Repository CreateRepository(string reference)
    {
        _logger.LogDebug("Creating ORAS repository for: {Reference}", reference);
        var parsedRef = Reference.Parse(reference);
        _logger.LogDebug(
            "Parsed reference: Registry={Registry}, Repository={Repository}, Reference={ContentReference}",
            parsedRef.Registry, parsedRef.Repository, parsedRef.ContentReference);

        var authClient = new Client(
            _httpClientProvider.GetClient(),
            _credentialProvider,
            new Cache(_cache));

        var repositoryOptions = new RepositoryOptions
        {
            Reference = parsedRef,
            Client = authClient
        };

        var repository = new Repository(repositoryOptions);
        return repository;
    }
}
