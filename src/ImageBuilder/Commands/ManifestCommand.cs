// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Base class for commands that operate on a manifest file.
/// </summary>
/// <typeparam name="TOptions">The options type for the command.</typeparam>
/// <typeparam name="TOptionsBuilder">The options builder type for the command.</typeparam>
public abstract class ManifestCommand<TOptions, TOptionsBuilder>(IManifestInfoProvider manifestInfoProvider)
    : Command<TOptions, TOptionsBuilder>, IManifestCommand
    where TOptions : ManifestOptions, new()
    where TOptionsBuilder : ManifestOptionsBuilder, new()
{
    private readonly IManifestInfoProvider _manifestInfoProvider = manifestInfoProvider;

    /// <summary>
    /// Gets the loaded manifest information.
    /// </summary>
    public ManifestInfo Manifest => _manifestInfoProvider.Manifest;

    /// <inheritdoc/>
    public virtual void LoadManifest()
    {
        _manifestInfoProvider.LoadManifest(Options);
    }

    protected override void Initialize(TOptions options)
    {
        base.Initialize(options);
        LoadManifest();
    }
}
