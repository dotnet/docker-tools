// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Provides access to the loaded manifest information for commands that need it.
/// This is distinct from <see cref="IManifestService"/> which handles Docker registry manifest operations.
/// </summary>
public interface IManifestInfoProvider
{
    /// <summary>
    /// Gets the loaded manifest information.
    /// </summary>
    /// <remarks>
    /// Must be initialized by calling <see cref="LoadManifest"/> before accessing.
    /// </remarks>
    ManifestInfo Manifest { get; }

    /// <summary>
    /// Loads the manifest from the specified options.
    /// </summary>
    /// <param name="options">The manifest options containing the manifest path and filters.</param>
    void LoadManifest(IManifestOptionsInfo options);
}
