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
    public AcrConfiguration? BuildAcr { get; set; }

    /// <summary>
    /// Images are copied from <see cref="BuildAcr"/> to this registry during publishing.
    /// </summary>
    public AcrConfiguration? PublishAcr { get; set; }

    /// <summary>
    /// External image dependencies are mirrored to this registry.
    /// </summary>
    public AcrConfiguration? InternalMirrorAcr { get; set; }

    /// <summary>
    /// External images are mirrored to this registry. This registry has anonymous pull access
    /// enabled so that it can be used in public PR validation.
    /// </summary>
    public AcrConfiguration? PublicMirrorAcr { get; set; }

    /// <summary>
    /// Gets all ACR configurations that were provided in the publish configuration.
    /// </summary>
    public IEnumerable<AcrConfiguration> GetKnownAcrConfigurations()
    {
        if (BuildAcr is not null)
        {
            yield return BuildAcr;
        }
    }
}
