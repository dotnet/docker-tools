// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras.Registry.Remote.Auth;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <summary>
/// Adapts <see cref="IRegistryCredentialsProvider"/> to the ORAS <see cref="ICredentialProvider"/> interface.
/// </summary>
public class OrasCredentialProviderAdapter : ICredentialProvider
{
    private readonly IRegistryCredentialsProvider _credentialsProvider;
    private readonly IRegistryCredentialsHost? _credentialsHost;

    /// <summary>
    /// Creates a new adapter instance.
    /// </summary>
    /// <param name="credentialsProvider">The ImageBuilder credentials provider to wrap.</param>
    /// <param name="credentialsHost">Optional credentials host for registry-specific credential options.</param>
    public OrasCredentialProviderAdapter(
        IRegistryCredentialsProvider credentialsProvider,
        IRegistryCredentialsHost? credentialsHost = null)
    {
        _credentialsProvider = credentialsProvider;
        _credentialsHost = credentialsHost;
    }

    /// <inheritdoc/>
    public async Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken cancellationToken)
    {
        var creds = await _credentialsProvider.GetCredentialsAsync(hostname, _credentialsHost);

        if (creds is null)
        {
            return default;
        }

        return new Credential(creds.Username, creds.Password, RefreshToken: string.Empty, AccessToken: string.Empty);
    }
}
