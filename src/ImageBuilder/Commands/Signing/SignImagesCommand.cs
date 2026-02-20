// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

/// <summary>
/// Signs container images using ESRP via MicroBuild plugin.
/// Reads image references from a merged image-info.json file and signs each image.
/// </summary>
public class SignImagesCommand(
    ILogger<SignImagesCommand> logger,
    IBulkImageSigningService signingService,
    ISigningRequestGenerator signingRequestGenerator,
    IFileSystem fileSystem,
    IOptions<PublishConfiguration> publishConfigOptions)
    : Command<SignImagesOptions, SignImagesOptionsBuilder>
{
    private readonly PublishConfiguration _publishConfiguration = publishConfigOptions.Value;

    protected override string Description =>
        "Signs container images listed in the image info file using ESRP";

    public override async Task ExecuteAsync()
    {
        logger.LogInformation("SIGNING CONTAINER IMAGES");

        SigningConfiguration? signingConfig = _publishConfiguration.Signing;
        if (signingConfig is null || !signingConfig.Enabled)
        {
            logger.LogInformation("Signing is not enabled. Skipping image signing.");
            return;
        }

        if (!fileSystem.FileExists(Options.ImageInfoPath))
        {
            string warning = PipelineHelper.FormatWarningCommand("Image info file not found. Skipping image signing.");
            logger.LogWarning(warning);
            return;
        }

        if (Options.IsDryRun)
        {
            logger.LogInformation("Dry run enabled. Skipping image signing.");
            return;
        }

        string imageInfoContents = await fileSystem.ReadAllTextAsync(Options.ImageInfoPath);
        ImageArtifactDetails imageArtifactDetails = ImageArtifactDetails.FromJson(imageInfoContents);

        logger.LogDebug(
            "Registry override: Registry='{Registry}', RepoPrefix='{RepoPrefix}'",
            Options.RegistryOverride.Registry, Options.RegistryOverride.RepoPrefix);

        // Apply registry override to get fully-qualified image references
        imageArtifactDetails = imageArtifactDetails.ApplyRegistryOverride(Options.RegistryOverride);

        IReadOnlyList<ImageSigningRequest> platformRequests =
            await signingRequestGenerator.GeneratePlatformSigningRequestsAsync(imageArtifactDetails);
        IReadOnlyList<ImageSigningRequest> manifestListRequests =
            await signingRequestGenerator.GenerateManifestListSigningRequestsAsync(imageArtifactDetails);

        List<ImageSigningRequest> allRequests = [..platformRequests, ..manifestListRequests];

        if (allRequests.Count == 0)
        {
            logger.LogInformation("No images to sign.");
            return;
        }

        int keyCode = signingConfig.ImageSigningKeyCode;
        logger.LogInformation(
            "Signing {Count} image(s) ({PlatformCount} platforms, {ManifestCount} manifest lists) with key code {KeyCode}.",
            allRequests.Count, platformRequests.Count, manifestListRequests.Count, keyCode);

        IReadOnlyList<ImageSigningResult> results = await signingService.SignImagesAsync(allRequests, keyCode);

        logger.LogInformation("Successfully signed {Count} image(s).", results.Count);
        foreach (ImageSigningResult result in results)
            logger.LogInformation(
                "{ImageName}: signature digest {SignatureDigest}",
                result.ImageName, result.SignatureDigest);
    }
}
