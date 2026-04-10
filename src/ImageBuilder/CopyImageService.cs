// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Oras;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.ImageBuilder;

public interface ICopyImageService
{
    Task ImportImageAsync(
        string[] destTagNames,
        string destAcrName,
        string srcTagName,
        bool copyReferrers,
        string? srcRegistryName = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null,
        bool isDryRun = false);
}

public class CopyImageService : ICopyImageService
{
    private readonly ILogger<CopyImageService> _logger;
    private readonly IAcrImageImporter _acrRegistryImporter;
    private readonly IOrasService _orasService;
    private readonly PublishConfiguration _publishConfig;

    public CopyImageService(
        ILogger<CopyImageService> logger,
        IAcrImageImporter acrRegistryImporter,
        IOrasService orasService,
        IOptions<PublishConfiguration> publishConfigOptions)
    {
        _logger = logger;
        _acrRegistryImporter = acrRegistryImporter;
        _orasService = orasService;
        _publishConfig = publishConfigOptions.Value;
    }

    public async Task ImportImageAsync(
        string[] destTagNames,
        string destAcrName,
        string srcTagName,
        bool copyReferrers,
        string? srcRegistryName = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null,
        bool isDryRun = false)
    {
        Acr destAcr = Acr.Parse(destAcrName);

        string sourceImageName = DockerHelper.GetImageName(srcRegistryName, srcTagName);
        string destRepo = destTagNames.First().Split(':')[0].Split('@')[0];

        IReadOnlyList<ReferrerInfo> referrers;
        if (copyReferrers)
        {
            // Discover referrers (signatures, SBOMs, etc.) for the source image.
            referrers = await _orasService.GetReferrersAsync(reference: sourceImageName, isDryRun: isDryRun);
        }
        else
        {
            referrers = [];
            _logger.LogInformation("Skipping referrer discovery for '{SourceImage}' (copyReferrers=false)", sourceImageName);
        }

        var destinationImageNames =
            destTagNames.Select(tag => $"'{DockerHelper.GetImageName(destAcr.Server, tag)}'").ToList();
        string formattedDestinationImages = string.Join(", ", destinationImageNames);

        _logger.LogInformation(
            "Importing {DestinationImages} and {ReferrerCount} referrer(s) from '{SourceImage}' (DryRun={DryRun})",
            formattedDestinationImages, referrers.Count, sourceImageName, isDryRun);

        if (isDryRun)
        {
            return;
        }

        ResourceIdentifier destResourceId = _publishConfig.GetAcrResource(destAcrName);

        // Only look up the source resource ID for registries in the publish config (i.e. ACRs).
        // External registries like docker.io use RegistryAddress + Credentials instead.
        ResourceIdentifier? srcResourceId =
            // TODO: In the future, ACR credentials (user/pw) could be passed via PublishConfiguration
            // as well, and resolved here.
            srcRegistryName is not null && _publishConfig.FindRegistryAuthentication(srcRegistryName) is not null
                ? _publishConfig.GetAcrResource(srcRegistryName)
                : null;

        ContainerRegistryImportSource importSrc = new(srcTagName)
        {
            ResourceId = srcResourceId,
            RegistryAddress = srcResourceId is null ? srcRegistryName : null,
            Credentials = sourceCredentials
        };

        ContainerRegistryImportImageContent importImageContent = new(importSrc)
        {
            Mode = ContainerRegistryImportMode.Force
        };

        importImageContent.TargetTags.AddRange(destTagNames);

        await _acrRegistryImporter.ImportImageAsync(destAcrName, destResourceId, importImageContent);

        foreach (ReferrerInfo referrer in referrers)
        {
            string referrerDigestReference = DockerHelper.TrimRegistry(referrer.Digest, srcRegistryName);
            ContainerRegistryImportSource referrerImportSrc = new(referrerDigestReference)
            {
                ResourceId = srcResourceId,
                RegistryAddress = srcResourceId is null ? srcRegistryName : null,
                Credentials = sourceCredentials
            };

            ContainerRegistryImportImageContent referrerImportContent = new(referrerImportSrc)
            {
                Mode = ContainerRegistryImportMode.Force,
            };
            referrerImportContent.UntaggedTargetRepositories.Add(destRepo);

            await _acrRegistryImporter.ImportImageAsync(destAcrName, destResourceId, referrerImportContent);
        }
    }
}
