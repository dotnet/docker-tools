// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager.ContainerRegistry.Models;


#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands;

public abstract class CopyImagesCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
    where TOptions : CopyImagesOptions, new()
    where TOptionsBuilder : CopyImagesOptionsBuilder, new()
{
    private readonly ICopyImageService _copyImageService;

    public CopyImagesCommand(ICopyImageService copyImageService, ILoggerService loggerService)
    {
        _copyImageService = copyImageService;
        LoggerService = loggerService;
    }

    public ILoggerService LoggerService { get; }

    protected Task ImportImageAsync(
        string destTagName,
        string destRegistryName,
        string srcTagName,
        string? srcRegistryName = null,
        ResourceIdentifier? srcResourceId = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null) =>
            _copyImageService.ImportImageAsync(
                Options.Subscription,
                Options.ResourceGroup,
                [destTagName],
                destRegistryName,
                srcTagName,
                srcRegistryName,
                srcResourceId,
                sourceCredentials,
                Options.IsDryRun);
}
