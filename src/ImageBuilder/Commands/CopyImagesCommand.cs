// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands;

public abstract class CopyImagesCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
    where TOptions : CopyImagesOptions, new()
    where TOptionsBuilder : CopyImagesOptionsBuilder, new()
{
    private readonly ICopyImageService _copyImageService;
    private readonly PublishConfiguration _publishConfig;

    public CopyImagesCommand(
        ICopyImageService copyImageService,
        ILoggerService loggerService,
        IOptions<PublishConfiguration> publishConfigOptions)
    {
        LoggerService = loggerService;
        _copyImageService = copyImageService;
        _publishConfig = publishConfigOptions.Value;
    }

    protected ILoggerService LoggerService { get; }

    /// <summary>
    /// Gets the subscription for the destination registry from config.
    /// </summary>
    protected string GetDestinationSubscription()
    {
        var auth = _publishConfig.FindRegistryAuthentication(Manifest.Registry);
        if (auth?.Subscription is null)
        {
            throw new InvalidOperationException(
                $"No subscription found for registry '{Manifest.Registry}'. " +
                $"Ensure the registry is configured in the publish configuration.");
        }

        return auth.Subscription;
    }

    /// <summary>
    /// Gets the resource group for the destination registry from config.
    /// </summary>
    protected string GetDestinationResourceGroup()
    {
        var auth = _publishConfig.FindRegistryAuthentication(Manifest.Registry);
        if (auth?.ResourceGroup is null)
        {
            throw new InvalidOperationException(
                $"No resource group found for registry '{Manifest.Registry}'. " +
                $"Ensure the registry is configured in the publish configuration.");
        }

        return auth.ResourceGroup;
    }

    protected Task ImportImageAsync(
        string destTagName,
        string destRegistryName,
        string srcTagName,
        string? srcRegistryName = null,
        ResourceIdentifier? srcResourceId = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null) =>
            _copyImageService.ImportImageAsync(
                GetDestinationSubscription(),
                GetDestinationResourceGroup(),
                [destTagName],
                destRegistryName,
                srcTagName,
                srcRegistryName,
                srcResourceId,
                sourceCredentials,
                Options.IsDryRun);
}
