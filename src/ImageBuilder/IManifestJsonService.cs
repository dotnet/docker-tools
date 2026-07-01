// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Loads and parses manifest JSON files (<c>manifest.json</c>) into <see cref="ManifestInfo"/> view models.
/// </summary>
/// <remarks>
/// <para>
/// A manifest JSON file is the primary metadata source that defines which Docker images to build,
/// their tags, platforms, and dependencies. This service reads the file from disk, processes any
/// include files, validates the schema, and produces a fully initialized <see cref="ManifestInfo"/>
/// object graph.
/// </para>
/// <para>
/// This is distinct from <see cref="IManifestService"/>, which queries Docker registries for
/// OCI/Docker image manifests (a different use of the word "manifest").
/// </para>
/// </remarks>
public interface IManifestJsonService
{
    /// <summary>
    /// Loads a manifest JSON file and returns the parsed <see cref="ManifestInfo"/> view model.
    /// </summary>
    /// <param name="options">
    /// Options that specify the manifest file path, filter criteria, registry overrides,
    /// and template variables used during manifest loading.
    /// </param>
    /// <returns>A fully initialized <see cref="ManifestInfo"/> with repos, images, and platforms resolved.</returns>
    ManifestInfo Load(IManifestOptionsInfo options);
}
