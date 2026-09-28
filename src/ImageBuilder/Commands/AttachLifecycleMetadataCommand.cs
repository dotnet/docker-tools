// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Annotates unsupported images with EOL lifecycle artifacts. The <c>published</c> subcommand annotates published
/// images that were replaced or removed. The <c>all</c> subcommand annotates every non-referrer artifact in a
/// registry. Both support <c>--mark-as-internal</c> to keep lifecycle artifacts from being copied when publishing.
/// </summary>
public class AttachLifecycleMetadataCommand(
    ILogger<AttachLifecycleMetadataCommand> logger,
    IAcrClientFactory acrClientFactory,
    IAcrContentClientFactory acrContentClientFactory,
    ILifecycleMetadataService lifecycleMetadataService,
    IMarImageIngestionReporter ingestionReporter,
    IArtifactService artifactService)
    : ICommand
{
    public Command GetCliCommand()
    {
        UnsupportedLifecycleMetadataOptions publishedOptions = new();
        AttachLifecycleMetadataOptions allOptions = new();

        return new Command(name: this.GetCommandName(), description: "Attaches lifecycle metadata artifacts to images")
        {
            CommandAction.Create(
                name: "published",
                description: "Attaches EOL lifecycle metadata to images that were just replaced or removed and are thus no longer supported",
                options: publishedOptions,
                run: ct => AttachToUnsupportedAsync(publishedOptions, ct)),
            CommandAction.Create(
                name: "all",
                description: "Attaches EOL lifecycle metadata to every non-referrer artifact in the registry",
                options: allOptions,
                run: ct => AttachToAllAsync(allOptions, ct)),
        };
    }

    public async Task AttachToUnsupportedAsync(
        UnsupportedLifecycleMetadataOptions options,
        CancellationToken cancellationToken)
    {
        if (options.IsDryRun)
        {
            logger.LogInformation(
                "(Dry run) Skipping EOL annotation of images in {Registry}.",
                options.RegistryOptions.Registry);

            return;
        }

        string oldImageInfoPath = artifactService.ResolvePath(options.OldImageInfoPath);
        string newImageInfoPath = artifactService.ResolvePath(options.NewImageInfoPath);

        if (!File.Exists(oldImageInfoPath) && !File.Exists(newImageInfoPath))
        {
            logger.LogError("Cannot attach lifecycle metadata because no image info files were provided.");
            return;
        }

        ImageArtifactDetails oldImageInfo = ImageInfoHelper.DeserializeImageArtifactDetails(oldImageInfoPath);
        ImageArtifactDetails newImageInfo = ImageInfoHelper.DeserializeImageArtifactDetails(newImageInfoPath);

        IReadOnlyList<string> eolDigests =
            await GetUnsupportedImageDigestsAsync(
                oldImageInfo,
                newImageInfo,
                options.RegistryOptions,
                cancellationToken);

        IReadOnlyList<string> createdAnnotationDigests =
            await AttachLifecycleMetadataAsync(
                eolDigests,
                options.MarkAsInternal,
                cancellationToken);

        if (options.WaitForIngestion)
        {
            await ingestionReporter.ReportImageStatusesAsync(
                options.MarServiceConnection,
                createdAnnotationDigests.Select(ToDigestInfo),
                options.IngestionOptions.WaitTimeout,
                options.IngestionOptions.RequeryDelay,
                minimumQueueTime: null,
                cancellationToken);
        }
    }

    public async Task AttachToAllAsync(AttachLifecycleMetadataOptions options, CancellationToken cancellationToken)
    {
        string registry = options.RegistryOptions.Registry;

        if (options.IsDryRun)
        {
            logger.LogInformation("(Dry run) Skipping EOL annotation of images in {Registry}.", registry);
            return;
        }

        IReadOnlyList<string> eolDigests =
            await GetRegistryNonReferrerDigestsAsync(registry, _ => true, cancellationToken);

        await AttachLifecycleMetadataAsync(eolDigests, options.MarkAsInternal, cancellationToken);
    }

    /// <summary>
    /// Annotates each digest that doesn't already have a lifecycle artifact of the same kind, and returns the
    /// digests of the created annotations.
    /// </summary>
    private async Task<IReadOnlyList<string>> AttachLifecycleMetadataAsync(
        IReadOnlyList<string> eolDigests,
        bool markAsInternal,
        CancellationToken cancellationToken)
    {
        DateOnly eolDate = DateOnly.FromDateTime(DateTime.UtcNow);
        ConcurrentBag<string> createdAnnotationDigests = [];
        ConcurrentBag<string> failedDigests = [];

        await Parallel.ForEachAsync(eolDigests, cancellationToken, async (digest, ct) =>
        {
            // Internal annotations only suppress vulnerability scanning, so any existing lifecycle artifact is
            // enough. Public annotations must not be skipped because of an internal one.
            LifecycleArtifact? existingArtifact = await lifecycleMetadataService
                .GetLatestLifecycleArtifactAsync(digest, includeInternal: markAsInternal, ct);

            if (existingArtifact is not null)
            {
                logger.LogDebug("Skipping '{Digest}' because it already has a lifecycle artifact.", digest);
                return;
            }

            logger.LogInformation(
                "Annotating EOL for digest '{Digest}', date '{EolDate}', internal '{MarkAsInternal}'",
                digest,
                eolDate,
                markAsInternal);

            LifecycleArtifact? createdArtifact = await lifecycleMetadataService
                .AnnotateEolDigestAsync(digest, eolDate, markAsInternal, ct);

            if (createdArtifact is null)
            {
                failedDigests.Add(digest);
            }
            else
            {
                createdAnnotationDigests.Add(createdArtifact.Referrer.Digest);
            }
        });

        logger.LogInformation(
            "Created {CreatedCount} EOL annotation(s) for {EolCount} unsupported image(s).",
            createdAnnotationDigests.Count,
            eolDigests.Count);

        if (!failedDigests.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to annotate {failedDigests.Count} digest(s):{Environment.NewLine}"
                    + string.Join(Environment.NewLine, failedDigests.Order()));
        }

        return [.. createdAnnotationDigests];
    }

    /// <summary>
    /// Gets the registry digests of images that are no longer described by the new image info file.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetUnsupportedImageDigestsAsync(
        ImageArtifactDetails oldImageInfo,
        ImageArtifactDetails newImageInfo,
        RegistryOptions registryOptions,
        CancellationToken cancellationToken)
    {
        // Only query repos described by the image info files, since other repos in the registry may be owned by
        // other image info files. The old image info is included so that repos removed entirely are still in scope.
        HashSet<string> repoNames = newImageInfo.Repos
            .Select(repo => repo.Repo)
            .Union(oldImageInfo.Repos.Select(repo => repo.Repo))
            .Select(name => registryOptions.RepoPrefix + name)
            .ToHashSet();

        IReadOnlyList<string> registryDigests = await GetRegistryNonReferrerDigestsAsync(
            registryOptions.Registry,
            repoNames.Contains,
            cancellationToken);

        HashSet<string> supportedDigests = newImageInfo
            .ApplyRegistryOverride(registryOptions)
            .GetAllDigests()
            .ToHashSet();

        return registryDigests.Where(digest => !supportedDigests.Contains(digest)).ToList();
    }

    /// <summary>
    /// Gets the fully-qualified digests of all images and manifest lists in the matching registry repos,
    /// excluding referrer artifacts.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetRegistryNonReferrerDigestsAsync(
        string registry,
        Func<string, bool> repoFilter,
        CancellationToken cancellationToken)
    {
        IAcrClient acrClient = acrClientFactory.Create(registry);

        IAsyncEnumerable<string> repositoryNames = acrClient
            .GetRepositoryNamesAsync(cancellationToken)
            .Where(repoFilter);

        ConcurrentBag<string> digests = [];

        await Parallel.ForEachAsync(
            repositoryNames,
            cancellationToken,
            async (repositoryName, outerCT) =>
            {
                IAcrContentClient contentClient = acrContentClientFactory.Create(Acr.Parse(registry), repositoryName);
                ContainerRepository repo = acrClient.GetRepository(repositoryName);

                await Parallel.ForEachAsync(
                    repo.GetAllManifestPropertiesAsync(cancellationToken: outerCT),
                    outerCT,
                    async (manifestProps, innerCT) =>
                    {
                        ManifestQueryResult manifestResult;

                        try
                        {
                            manifestResult = await contentClient.GetManifestAsync(manifestProps.Digest, innerCT);
                        }
                        catch (RequestFailedException ex) when (ex.Status == 404)
                        {
                            // Images can be cleaned up concurrently between listing and fetching.
                            logger.LogWarning(
                                "Manifest {Digest} in {Repository} was listed but no longer exists. Skipping.",
                                manifestProps.Digest,
                                repositoryName);

                            return;
                        }

                        // Do not attach lifecycle metadata to other lifecycle metadata, otherwise
                        // we risk creating an unbounded number of artifacts.
                        if (!manifestResult.IsReferrer())
                        {
                            string imageName = DockerHelper.GetImageName(
                                registry: registry,
                                repo: repositoryName,
                                digest: manifestProps.Digest);

                            digests.Add(imageName);
                        }
                    });
            });

        return [.. digests];
    }

    private static DigestInfo ToDigestInfo(string digestReference)
    {
        ImageName name = ImageName.Parse(digestReference);
        return new DigestInfo(name.Digest, name.Repo, tags: []);
    }
}
