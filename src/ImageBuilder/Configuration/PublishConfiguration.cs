// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record PublishConfiguration
{
    public static string ConfigurationKey => nameof(PublishConfiguration);

    /// <summary>
    /// Images are built and pushed into this registry before testing and publishing.
    /// </summary>
    public RegistryConfiguration? BuildRegistry { get; set; }

    /// <summary>
    /// Images are copied from <see cref="BuildRegistry"/> to this registry during publishing.
    /// </summary>
    public RegistryConfiguration? PublishRegistry { get; set; }

    /// <summary>
    /// External image dependencies are mirrored to this registry.
    /// </summary>
    public RegistryConfiguration? InternalMirrorRegistry { get; set; }

    /// <summary>
    /// External images are mirrored to this registry. This registry has anonymous pull access
    /// enabled so that it can be used in public PR validation.
    /// </summary>
    public RegistryConfiguration? PublicMirrorRegistry { get; set; }

    /// <summary>
    /// Gets all registries that were provided in the publish configuration.
    /// </summary>
    public IEnumerable<RegistryConfiguration> GetKnownRegistries()
    {
        if (BuildRegistry is not null)
        {
            yield return BuildRegistry;
        }

        if (PublishRegistry is not null)
        {
            yield return PublishRegistry;
        }

        if (InternalMirrorRegistry is not null)
        {
            yield return InternalMirrorRegistry;
        }

        if (PublicMirrorRegistry is not null)
        {
            yield return PublicMirrorRegistry;
        }
    }
}
