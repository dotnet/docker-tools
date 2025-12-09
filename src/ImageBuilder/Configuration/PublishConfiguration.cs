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
    public RegistryConfiguration? BuildAcr { get; set; }

    /// <summary>
    /// Images are copied from <see cref="BuildAcr"/> to this registry during publishing.
    /// </summary>
    public RegistryConfiguration? PublishAcr { get; set; }

    /// <summary>
    /// External image dependencies are mirrored to this registry.
    /// </summary>
    public RegistryConfiguration? InternalMirrorAcr { get; set; }

    /// <summary>
    /// External images are mirrored to this registry. This registry has anonymous pull access
    /// enabled so that it can be used in public PR validation.
    /// </summary>
    public RegistryConfiguration? PublicMirrorAcr { get; set; }

    /// <summary>
    /// Gets all ACR configurations that were provided in the publish configuration.
    /// </summary>
    public IEnumerable<RegistryConfiguration> GetKnownAcrConfigurations()
    {
        if (BuildAcr is not null)
        {
            yield return BuildAcr;
        }

        if (PublishAcr is not null)
        {
            yield return PublishAcr;
        }

        if (InternalMirrorAcr is not null)
        {
            yield return InternalMirrorAcr;
        }

        if (PublicMirrorAcr is not null)
        {
            yield return PublicMirrorAcr;
        }
    }
}
