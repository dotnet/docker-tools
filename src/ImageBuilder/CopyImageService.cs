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
        string? srcRegistryName = null,
        ContainerRegistryImportSourceCredentials? sourceCredentials = null,
        bool isDryRun = false)
    {
        Acr destAcr = Acr.Parse(destAcrName);

        string action = isDryRun ? "(Dry run) Would have imported" : "Importing";
        string sourceImageName = DockerHelper.GetImageName(srcRegistryName, srcTagName);
        var destinationImageNames = destTagNames
            .Select(tag => $"'{DockerHelper.GetImageName(destAcr.Name, tag)}'")
            .ToList();
        string formattedDestinationImages = string.Join(", ", destinationImageNames);
        _logger.LogInformation("{Action} {DestinationImages} from '{SourceImage}'",
            action, formattedDestinationImages, sourceImageName);

        if (isDryRun)
        {
            _logger.LogInformation("Importing skipped due to dry run.");
            return;
        }

        var destResourceId = _publishConfig.GetAcrResource(destAcrName);

        // Only look up the source resource ID for registries in the publish config (i.e. ACRs).
        // External registries like docker.io use RegistryAddress + Credentials instead.
        ResourceIdentifier? srcResourceId =
            // TODO: In the future, ACR credentials (user/pw) could be passed via PublishConfiguration
            // as well, and resolved here.
            srcRegistryName is not null && _publishConfig.FindRegistryAuthentication(srcRegistryName) is not null
                ? _publishConfig.GetAcrResource(srcRegistryName)
                : null;

        // Azure ACR import only supports one source identifier. Use ResourceId for ACR-to-ACR
        // imports (same tenant), or RegistryAddress for external registries.
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

        // Discover and import all OCI referrers (signatures, SBOMs, etc.) for the source image.
        string sourceImageReference = DockerHelper.GetImageName(srcRegistryName, srcTagName);
        IReadOnlyList<ReferrerInfo> referrers = await _orasService.GetReferrersAsync(sourceImageReference);

        string destRepo = destTagNames.First().Split(':')[0].Split('@')[0];
        foreach (ReferrerInfo referrer in referrers)
        {
            _logger.LogInformation("Importing referrer '{Referrer}' to '{DestAcr}/{DestRepo}'", referrer.Digest, destAcrName, destRepo);

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
