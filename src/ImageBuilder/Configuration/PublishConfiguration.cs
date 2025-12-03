// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public sealed record PublishConfiguration
{
    public static string ConfigurationKey => nameof(PublishConfiguration);

    public AcrConfiguration? BuildAcr { get; set; }

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
