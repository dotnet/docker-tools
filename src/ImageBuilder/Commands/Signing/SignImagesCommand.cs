// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

/// <summary>
/// Signs container images using ESRP via MicroBuild plugin.
/// Reads image references from a merged image-info.json file and signs each image.
/// </summary>
public class SignImagesCommand(
    ILoggerService loggerService,
    IBulkImageSigningService signingService,
    ISigningRequestGenerator signingRequestGenerator,
    IOptions<PublishConfiguration> publishConfigOptions)
    : Command<SignImagesOptions, SignImagesOptionsBuilder>
{
    private readonly PublishConfiguration _publishConfiguration = publishConfigOptions.Value;

    protected override string Description =>
        "Signs container images listed in the image info file using ESRP";

    public override async Task ExecuteAsync()
    {
        loggerService.WriteHeading("SIGNING CONTAINER IMAGES");

        var signingConfig = _publishConfiguration.Signing;
        if (signingConfig is null || !signingConfig.Enabled)
        {
            loggerService.WriteMessage("Signing is not enabled. Skipping image signing.");
            return;
        }

        if (!File.Exists(Options.ImageInfoPath))
        {
            loggerService.WriteMessage(PipelineHelper.FormatWarningCommand(
                "Image info file not found. Skipping image signing."));
            return;
        }

        if (Options.IsDryRun)
        {
            loggerService.WriteMessage("Dry run enabled. Skipping actual signing.");
            return;
        }

        var imageInfoContents = await File.ReadAllTextAsync(Options.ImageInfoPath);
        var imageArtifactDetails = ImageArtifactDetails.FromJson(imageInfoContents);

        // Apply registry override to get fully-qualified image references
        imageArtifactDetails = imageArtifactDetails.ApplyRegistryOverride(Options.RegistryOverride);

        var platformRequests = await signingRequestGenerator.GeneratePlatformSigningRequestsAsync(imageArtifactDetails);
        var manifestListRequests = await signingRequestGenerator.GenerateManifestListSigningRequestsAsync(imageArtifactDetails);
        var allRequests = platformRequests.Concat(manifestListRequests).ToList();

        if (allRequests.Count == 0)
        {
            loggerService.WriteMessage("No images to sign.");
            return;
        }

        var keyCode = signingConfig.ImageSigningKeyCode;
        loggerService.WriteMessage($"Signing {allRequests.Count} image(s) ({platformRequests.Count} platforms, {manifestListRequests.Count} manifest lists) with key code {keyCode}...");

        var results = await signingService.SignImagesAsync(allRequests, keyCode);

        loggerService.WriteMessage($"Successfully signed {results.Count} image(s).");
        foreach (var result in results)
        {
            loggerService.WriteMessage($"  {result.ImageName}: signature digest {result.SignatureDigest}");
        }
    }
}
