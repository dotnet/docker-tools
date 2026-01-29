// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;


/// <summary>
/// Implementation of <see cref="IRegistryResolver"/> that centralizes the logic
/// for determining how to authenticate with a given container registry.
/// </summary>
public class RegistryResolver(IOptions<PublishConfiguration> publishConfigOptions) : IRegistryResolver
{
    private readonly PublishConfiguration _publishConfig = publishConfigOptions.Value;

    /// <inheritdoc />
    public RegistryInfo Resolve(string registry, IRegistryCredentialsHost? credsHost)
    {
        var explicitCreds = credsHost?.TryGetCredentials(registry);

        // Docker Hub's registry has a separate host name for its API
        if (registry == DockerHelper.DockerHubRegistry)
        {
            // This is definitely not an ACR, so don't bother checking ACRs
            // passed in via the publish configuration.
            return new RegistryInfo(
                EffectiveRegistry: DockerHelper.DockerHubApiRegistry,
                OwnedAcr: null,
                ExplicitCredentials: explicitCreds);
        }

        // Look up the ACR in the publish configuration
        var ownedAcr = _publishConfig.FindOwnedAcrByName(registry);

        return new RegistryInfo(
            EffectiveRegistry: registry,
            OwnedAcr: ownedAcr,
            ExplicitCredentials: explicitCreds);
    }
}
