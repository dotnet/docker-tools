// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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
        var repo = CreateRepository(reference);
        return await repo.ResolveAsync(reference, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> PushSignatureAsync(
        Descriptor subjectDescriptor,
        PayloadSigningResult result,
        CancellationToken cancellationToken = default)
    {
        var repo = CreateRepository(result.ImageName);

        var annotations = new Dictionary<string, string>
        {
            [CertificateChainAnnotation] = result.CertificateChain
        };

        var options = new PackManifestOptions
        {
            ManifestAnnotations = annotations,
            Subject = subjectDescriptor
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
        var parsedRef = Reference.Parse(reference);
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
