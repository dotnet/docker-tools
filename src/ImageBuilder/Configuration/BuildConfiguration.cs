// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Configuration;

/// <summary>
/// Configuration for build and pipeline artifacts.
/// </summary>
public sealed record BuildConfiguration
{
    public static string ConfigurationKey => nameof(BuildConfiguration);

    /// <summary>
    /// Root directory for build artifacts. Signing payloads will be written to a subdirectory.
    /// </summary>
    public string? ArtifactStagingDirectory { get; set; }
}
