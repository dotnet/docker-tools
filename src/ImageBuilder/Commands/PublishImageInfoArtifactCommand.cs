// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class PublishImageInfoArtifactCommand(
    IManifestJsonService manifestJsonService,
    IImageInfoService imageInfoService,
    IOptions<PublishConfiguration> publishConfigOptions,
    ILogger<PublishImageInfoArtifactCommand> logger
) : ManifestCommand<PublishImageInfoArtifactOptions>(manifestJsonService)
{
    private readonly IImageInfoService _imageInfoService =
        imageInfoService ?? throw new ArgumentNullException(nameof(imageInfoService));

    private readonly PublishConfiguration _publishConfiguration =
        publishConfigOptions?.Value ?? throw new ArgumentNullException(nameof(publishConfigOptions));

    private readonly ILogger<PublishImageInfoArtifactCommand> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    protected override string Description => "Publishes a build's image info as an OCI artifact.";

    public override async Task ExecuteAsync()
    {
        _logger.LogInformation("Publishing image info as OCI artifact");

        RegistryEndpoint? publishRegistry = _publishConfiguration.PublishRegistry;

        if (string.IsNullOrWhiteSpace(publishRegistry?.Server))
        {
            throw new InvalidOperationException(
                "No publish registry is configured. Skipping image-info artifact push."
            );
        }

        byte[] imageInfoContent = File.ReadAllBytes(Options.ImageInfoPath);

        await _imageInfoService.PushImageInfoArtifactAsync(
            manifest: Manifest,
            imageInfoContent: imageInfoContent,
            registry: publishRegistry.Server,
            repoPrefix: publishRegistry.RepoPrefix,
            isDryRun: Options.IsDryRun);
    }
}

public class PublishImageInfoArtifactOptions : ImageInfoOptions
{
}
