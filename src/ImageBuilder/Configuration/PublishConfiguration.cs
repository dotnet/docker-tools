// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record PublishConfiguration
{
    public static string ConfigurationKey => nameof(PublishConfiguration);

    /// <summary>
    /// Images are built and pushed into this registry before testing and publishing.
    /// </summary>
    public RegistryEndpoint? BuildRegistry { get; set; }

    /// <summary>
    /// Images are copied from <see cref="BuildRegistry"/> to this registry during publishing.
    /// </summary>
    public RegistryEndpoint? PublishRegistry { get; set; }

    /// <summary>
    /// External image dependencies are mirrored to this registry.
    /// </summary>
    public RegistryEndpoint? InternalMirrorRegistry { get; set; }

    /// <summary>
    /// External images are mirrored to this registry. This registry has anonymous pull access
    /// enabled so that it can be used in public PR validation.
    /// </summary>
    public RegistryEndpoint? PublicMirrorRegistry { get; set; }

    /// <summary>
    /// Authentication details for container registries, keyed by registry server name.
    /// </summary>
    /// <remarks>
    /// The key should be the registry server address (e.g., "myregistry.azurecr.io").
    /// Multiple registry endpoints can share the same authentication by using the same key.
    /// </remarks>
    public Dictionary<string, RegistryAuthentication> RegistryAuthentication { get; set; } = new();

    /// <summary>
    /// Gets all registry endpoints that were provided in the publish configuration.
    /// </summary>
    public IEnumerable<RegistryEndpoint> GetKnownRegistries()
    {
        RegistryEndpoint?[] registries =
        [
            BuildRegistry,
            PublishRegistry,
            InternalMirrorRegistry,
            PublicMirrorRegistry
        ];

        // Use OfType to filter out null values, since Where(x => x is not null)
        // does not get rid of the nullable annotation on RegistryEndpoint?.
        return registries.OfType<RegistryEndpoint>();
    }
}
