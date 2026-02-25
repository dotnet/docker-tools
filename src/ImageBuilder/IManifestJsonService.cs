// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Loads and parses manifest JSON files into <see cref="ManifestInfo"/> view models.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="IManifestService"/>, which queries Docker registries for image manifests.
/// </remarks>
public interface IManifestJsonService
{
    /// <summary>
    /// Loads a manifest JSON file from the path specified in <paramref name="options"/> and returns the parsed result.
    /// </summary>
    ManifestInfo Load(IManifestOptionsInfo options);
}
