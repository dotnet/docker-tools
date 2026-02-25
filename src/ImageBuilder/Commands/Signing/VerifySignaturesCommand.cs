// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Notation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Commands.Signing;

/// <summary>
/// Verifies container image signatures using the notation CLI.
/// Reads image references from a merged image-info.json file and verifies each signature.
/// </summary>
public class VerifySignaturesCommand(
    ILogger<VerifySignaturesCommand> logger,
    INotationClient notationClient,
    IRegistryCredentialsProvider registryCredentialsProvider,
    IFileSystem fileSystem,
    IOptions<PublishConfiguration> publishConfigOptions)
    : Command<VerifySignaturesOptions, VerifySignaturesOptionsBuilder>
{
    private readonly PublishConfiguration _publishConfiguration = publishConfigOptions.Value;

    protected override string Description =>
        "Verifies container image signatures listed in the image info file using notation";

    public override async Task ExecuteAsync()
    {
        logger.LogInformation("VERIFYING CONTAINER IMAGE SIGNATURES");

        var signingConfig = _publishConfiguration.Signing;
        if (signingConfig is null || !signingConfig.Enabled)
        {
            logger.LogInformation("Signing is not enabled. Skipping signature verification.");
            return;
        }

        if (!fileSystem.FileExists(Options.ImageInfoPath))
        {
            string warning = PipelineHelper.FormatWarningCommand(
                "Image info file not found. Skipping signature verification.");
            logger.LogWarning("{Warning}", warning);
            return;
        }

        if (Options.IsDryRun)
        {
            logger.LogInformation("Dry run enabled. Skipping actual verification.");
            return;
        }

        SetupTrustConfiguration(signingConfig);

        var imageInfoContents = await fileSystem.ReadAllTextAsync(Options.ImageInfoPath);
        var imageArtifactDetails = ImageArtifactDetails.FromJson(imageInfoContents);

        imageArtifactDetails = imageArtifactDetails.ApplyRegistryOverride(Options.RegistryOverride);

        var imageReferences = GetAllImageReferences(imageArtifactDetails);

        if (imageReferences.Count == 0)
        {
            logger.LogInformation("No images to verify.");
            return;
        }

        await LoginToRegistriesAsync(imageReferences);

        logger.LogInformation("Verifying signatures for {Count} image(s)...", imageReferences.Count);

        ConcurrentBag<(string Reference, Exception Error)> failures = [];

        await Parallel.ForEachAsync(imageReferences, (reference, _) =>
        {
            try
            {
                logger.LogInformation("Verifying: {Reference}", reference);
                notationClient.Verify(reference, Options.IsDryRun);
                logger.LogInformation("OK: {Reference}", reference);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed: {Reference}", reference);
                failures.Add((reference, ex));
            }

            return ValueTask.CompletedTask;
        });

        if (failures.Count > 0)
        {
            logger.LogError("Signature verification failed for {Count} image(s)", failures.Count);
            foreach (var (reference, error) in failures)
            {
                logger.LogError("{Reference}: {Message}", reference, error.Message);
            }

            throw new InvalidOperationException(
                $"Signature verification failed for {failures.Count} of {imageReferences.Count} image(s).");
        }

        logger.LogInformation("Successfully verified signatures for {Count} image(s).", imageReferences.Count);
    }

    /// <summary>
    /// Authenticates the notation CLI to each unique registry found in the image references.
    /// Uses docker login since notation reads credentials from Docker's credential store.
    /// </summary>
    private async Task LoginToRegistriesAsync(List<string> imageReferences)
    {
        var registries = imageReferences
            .Select(r => r.Split('/')[0])
            .Distinct()
            .ToList();

        foreach (var registry in registries)
        {
            var credentials = await registryCredentialsProvider.GetCredentialsAsync(registry, credsHost: null);
            if (credentials is null)
            {
                logger.LogInformation("No credentials found for '{Registry}'. Notation may fail if the registry requires authentication.", registry);
                continue;
            }

            logger.LogInformation("Logging in to '{Registry}' for notation...", registry);
            DockerHelper.Login(credentials, registry, isDryRun: false);
        }
    }

    /// <summary>
    /// Configures notation trust by importing the baked-in root CA certificate and trust policy
    /// for the specified trust store name.
    /// </summary>
    private void SetupTrustConfiguration(SigningConfiguration signingConfig)
    {
        var trustStoreName = signingConfig.TrustStoreName;

        var certPath = Path.Combine(Options.TrustMaterialsPath, "certs", trustStoreName, "root-ca.crt");
        if (!fileSystem.FileExists(certPath))
        {
            throw new FileNotFoundException(
                $"Root CA certificate not found at '{certPath}'. " +
                $"Ensure the trust store name '{trustStoreName}' is valid.");
        }

        logger.LogInformation("Adding root CA certificate from '{CertPath}' to trust store '{TrustStoreName}'...", certPath, trustStoreName);
        notationClient.AddCertificate("ca", trustStoreName, certPath);

        var policyPath = Path.Combine(Options.TrustMaterialsPath, "policies", $"{trustStoreName}.json");
        if (!fileSystem.FileExists(policyPath))
        {
            throw new FileNotFoundException(
                $"Trust policy not found at '{policyPath}'. " +
                $"Ensure the trust store name '{trustStoreName}' is valid.");
        }

        logger.LogInformation("Importing trust policy from '{PolicyPath}'...", policyPath);
        notationClient.ImportTrustPolicy(policyPath);
    }

    /// <summary>
    /// Extracts all image references (platform digests and manifest list digests) from image-info.
    /// </summary>
    private static List<string> GetAllImageReferences(ImageArtifactDetails imageArtifactDetails)
    {
        var platformRefs = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .SelectMany(image => image.Platforms
                    .Where(platform => !string.IsNullOrEmpty(platform.Digest))
                    .Select(platform => platform.Digest)));

        var manifestRefs = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images
                .Where(image => image.Manifest is not null && !string.IsNullOrEmpty(image.Manifest.Digest))
                .Select(image => image.Manifest.Digest));

        return platformRefs.Concat(manifestRefs).ToList();
    }
}
