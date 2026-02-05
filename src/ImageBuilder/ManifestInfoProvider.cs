// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Provides access to the loaded manifest information for commands that need it.
/// This is distinct from <see cref="ManifestService"/> which handles Docker registry manifest operations.
/// </summary>
internal class ManifestInfoProvider : IManifestInfoProvider
{
    private ManifestInfo? _manifest;

    /// <inheritdoc/>
    public ManifestInfo Manifest =>
        _manifest ?? throw new InvalidOperationException("Manifest has not been loaded. Call LoadManifest first.");

    /// <inheritdoc/>
    public void LoadManifest(IManifestOptionsInfo options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (_manifest is null)
        {
            _manifest = ManifestInfo.Load(options);
        }
    }
}
