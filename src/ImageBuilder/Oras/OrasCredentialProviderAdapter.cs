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
/// <remarks>
/// Creates a new adapter instance.
/// </remarks>
/// <param name="credentialsProvider">The ImageBuilder credentials provider to wrap.</param>
/// <param name="credentialsHost">Optional credentials host for registry-specific credential options.</param>
public class OrasCredentialProviderAdapter(
    IRegistryCredentialsProvider credentialsProvider,
    IRegistryCredentialsHost? credentialsHost = null) : ICredentialProvider
{
    private readonly IRegistryCredentialsProvider _credentialsProvider = credentialsProvider;
    private readonly IRegistryCredentialsHost? _credentialsHost = credentialsHost;

    /// <inheritdoc/>
    public async Task<Credential> ResolveCredentialAsync(string hostname, CancellationToken cancellationToken)
    {
        RegistryCredentials? registryCredentials =
            await _credentialsProvider.GetCredentialsAsync(hostname, _credentialsHost);

        if (registryCredentials is null) return default;

        var orasCredential = new Credential(
            Username: registryCredentials.Username,
            Password: registryCredentials.Password,
            RefreshToken: string.Empty,
            AccessToken: string.Empty);

        return orasCredential;
    }
}
