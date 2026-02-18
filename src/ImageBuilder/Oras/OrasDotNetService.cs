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
    ILoggerService logger,
    IRegistryCredentialsHost? credentialsHost = null) : IOrasDescriptorService, IOrasSignatureService
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

    private readonly IRegistryCredentialsProvider _credentialsProvider = credentialsProvider;
    private readonly IRegistryCredentialsHost? _credentialsHost = credentialsHost;
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IMemoryCache _cache = cache;
    private readonly ILoggerService _logger = logger;

    /// <inheritdoc/>
    public async Task<Descriptor> GetDescriptorAsync(string reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        _logger.WriteMessage($"Resolving descriptor for reference: {reference}");
        var repo = CreateRepository(reference);
        var descriptor = await repo.ResolveAsync(reference, cancellationToken);
        _logger.WriteMessage($"Resolved descriptor: mediaType={descriptor.MediaType}, digest={descriptor.Digest}, size={descriptor.Size}");
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

        var repo = CreateRepository(result.ImageName);

        var payloadBytes = await File.ReadAllBytesAsync(result.SignedPayload.FullName, cancellationToken);
        var signatureLayerDescriptor = Descriptor.Create(payloadBytes, CoseMediaType);

        using var payloadStream = new MemoryStream(payloadBytes);
        await repo.PushAsync(signatureLayerDescriptor, payloadStream, cancellationToken);

        var annotations = new Dictionary<string, string>
        {
            [CertificateChainAnnotation] = result.CertificateChain
        };

        var options = new PackManifestOptions
        {
            ManifestAnnotations = annotations,
            Subject = subjectDescriptor,
            Layers = [signatureLayerDescriptor]
        };

        _logger.WriteMessage($"Pushing signature for {result.ImageName}");

        var signatureDescriptor = await Packer.PackManifestAsync(
            repo,
            Packer.ManifestVersion.Version1_1,
            NotarySignatureArtifactType,
            options,
            cancellationToken);

        _logger.WriteMessage($"Signature pushed: {signatureDescriptor.Digest}");

        return signatureDescriptor.Digest;
    }

    /// <summary>
    /// Creates an authenticated ORAS repository client for the given reference.
    /// </summary>
    /// <param name="reference">Full registry reference (e.g., "registry.io/repo:tag").</param>
    private Repository CreateRepository(string reference)
    {
        _logger.WriteMessage($"Creating ORAS repository for: {reference}");
        var parsedRef = Reference.Parse(reference);
        _logger.WriteMessage($"Parsed reference: Registry={parsedRef.Registry}, Repository={parsedRef.Repository}, Reference={parsedRef.ContentReference}");
        var credentialProvider = new OrasCredentialProviderAdapter(_credentialsProvider, _credentialsHost);
        var authClient = new Client(
            _httpClientProvider.GetClient(),
            credentialProvider,
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
