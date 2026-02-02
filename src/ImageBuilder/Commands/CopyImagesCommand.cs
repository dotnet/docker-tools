// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public abstract class CopyImagesCommand<TOptions, TOptionsBuilder>(
    ICopyImageService copyImageService,
    ILoggerService loggerService)
    : ManifestCommand<TOptions, TOptionsBuilder>
        where TOptions : CopyImagesOptions, new()
        where TOptionsBuilder : CopyImagesOptionsBuilder, new()
{
    private readonly ICopyImageService _copyImageService = copyImageService;

    protected ILoggerService LoggerService { get; } = loggerService;

    protected Task ImportImageAsync(
        string destTagName,
        string destRegistryName,
        string srcTagName,
        string? srcRegistryName = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null) =>
            _copyImageService.ImportImageAsync(
                destTagNames: [destTagName],
                destAcrName: destRegistryName,
                srcTagName: srcTagName,
                srcRegistryName: srcRegistryName,
                sourceCredentials: sourceCredentials,
                isDryRun: Options.IsDryRun);
}
