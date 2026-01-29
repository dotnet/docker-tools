// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// ORAS .NET library implementation for resolving OCI descriptors.
/// </summary>
public class OrasDotNetDescriptorService : IOrasDescriptorService
{
    private readonly IRegistryCredentialsProvider _credentialsProvider;
    private readonly IRegistryCredentialsHost? _credentialsHost;
    private readonly IHttpClientProvider _httpClientProvider;
    private readonly IMemoryCache _cache;

    public OrasDotNetDescriptorService(
        IRegistryCredentialsProvider credentialsProvider,
        IHttpClientProvider httpClientProvider,
        IMemoryCache cache,
        IRegistryCredentialsHost? credentialsHost = null)
    {
        _credentialsProvider = credentialsProvider;
        _httpClientProvider = httpClientProvider;
        _cache = cache;
        _credentialsHost = credentialsHost;
    }

    /// <inheritdoc/>
    public async Task<Descriptor> GetDescriptorAsync(string reference, CancellationToken cancellationToken = default)
    {
        var repo = CreateRepository(reference);
        return await repo.ResolveAsync(reference, cancellationToken);
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

        return new Repository(new RepositoryOptions
        {
            Reference = parsedRef,
            Client = authClient
        });
    }
}
