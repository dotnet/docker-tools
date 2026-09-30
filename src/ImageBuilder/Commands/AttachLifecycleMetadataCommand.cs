// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Annotates unsupported images with EOL lifecycle artifacts. The <c>published</c> subcommand annotates published
/// images that were replaced or removed. The <c>all</c> subcommand annotates every non-referrer artifact in a
/// registry. The <c>file</c> subcommand attaches metadata to explicit digests and dates from JSON.
/// <c>published</c> and <c>all</c> skip images that already have lifecycle metadata with any EOL date, while
/// <c>file</c> warns about (or, with <c>--stop-on-conflict</c>, fails on) existing metadata with a different date.
/// All support <c>--mark-as-internal</c> to keep lifecycle artifacts from being copied when publishing.
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
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Command GetCliCommand()
    {
        UnsupportedLifecycleMetadataOptions publishedOptions = new();
        RegistryLifecycleMetadataOptions allOptions = new();
        FileLifecycleMetadataOptions fileOptions = new();

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
            CommandAction.Create(
                name: "file",
                description: "Attaches EOL lifecycle metadata to digests specified in a JSON file",
                options: fileOptions,
                run: ct => AttachFromFileAsync(fileOptions, ct)),
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

        DateOnly eolDate = DateOnly.FromDateTime(DateTime.UtcNow);
        IReadOnlyList<string> createdAnnotationDigests =
            await AttachLifecycleMetadataAsync(
                eolDigests.Select(digest => new EolDigestData { Digest = digest, EolDate = eolDate }).ToArray(),
                options.MarkAsInternal,
                DateMismatchHandling.Ignore,
                cancellationToken);

        await WaitForIngestionAsync(
            createdAnnotationDigests, options.WaitForIngestion, options.MarServiceConnection,
            options.IngestionOptions, cancellationToken);
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

    public async Task AttachToAllAsync(RegistryLifecycleMetadataOptions options, CancellationToken cancellationToken)
    {
        string registry = options.RegistryOptions.Registry;

        if (options.IsDryRun)
        {
            logger.LogInformation("(Dry run) Skipping EOL annotation of images in {Registry}.", registry);
            return;
        }

        IReadOnlyList<string> eolDigests =
            await GetRegistryNonReferrerDigestsAsync(registry, _ => true, cancellationToken);

        DateOnly eolDate = DateOnly.FromDateTime(DateTime.UtcNow);

        IReadOnlyList<string> createdAnnotationDigests = await AttachLifecycleMetadataAsync(
            eolDigests.Select(digest => new EolDigestData { Digest = digest, EolDate = eolDate }).ToArray(),
            options.MarkAsInternal,
            DateMismatchHandling.Ignore,
            cancellationToken);

        await WaitForIngestionAsync(
            createdAnnotationDigests,
            options.WaitForIngestion,
            options.MarServiceConnection,
            options.IngestionOptions,
            cancellationToken);
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

    public async Task AttachFromFileAsync(FileLifecycleMetadataOptions options, CancellationToken cancellationToken)
    {
        string path = artifactService.ResolvePath(options.EolDigestsListPath);
        string jsonString = await File.ReadAllTextAsync(path, cancellationToken);

        EolAnnotationsData data = JsonSerializer.Deserialize<EolAnnotationsData>(jsonString, s_jsonOptions)
            ?? throw new JsonException($"Unable to deserialize EOL annotation data from '{path}'.");

        List<EolDigestData> eolDigests = data.EolDigests
            .Select(digestData =>
            {
                if (string.IsNullOrWhiteSpace(digestData.Digest))
                {
                    throw new InvalidOperationException($"An image digest is missing from '{path}'.");
                }

                return digestData with
                {
                    EolDate = digestData.EolDate
                        ?? data.EolDate
                        ?? throw new InvalidOperationException(
                            $"EOL date is not specified for digest '{digestData.Digest}'."),
                };
            })
            .ToList();

        if (options.IsDryRun)
        {
            logger.LogInformation(
                "(Dry run) Skipping lifecycle metadata attachment for {DigestCount} digest(s) from '{Path}'.",
                eolDigests.Count,
                path);

            return;
        }

        IReadOnlyList<string> createdAnnotationDigests = await AttachLifecycleMetadataAsync(
            eolDigests,
            options.MarkAsInternal,
            options.StopOnConflict ? DateMismatchHandling.Fail : DateMismatchHandling.Warn,
            cancellationToken);

        await WaitForIngestionAsync(
            createdAnnotationDigests,
            options.WaitForIngestion,
            options.MarServiceConnection,
            options.IngestionOptions,
            cancellationToken);
    }

    /// <summary>
    /// Attaches lifecycle metadata and returns only newly created artifact digests.
    /// </summary>
    private async Task<IReadOnlyList<string>> AttachLifecycleMetadataAsync(
        IReadOnlyList<EolDigestData> eolDigests,
        bool markAsInternal,
        DateMismatchHandling dateMismatchHandling,
        CancellationToken cancellationToken)
    {
        ConcurrentBag<string> createdAnnotationDigests = [];

        await Parallel.ForEachAsync(eolDigests, cancellationToken, async (digestData, ct) =>
        {
            LifecycleMetadataAttachmentResult result =
                await lifecycleMetadataService.AttachLifecycleMetadataAsync(
                    digestData.Digest,
                    digestData.EolDate!.Value,
                    markAsInternal,
                    stopOnConflict: dateMismatchHandling == DateMismatchHandling.Fail,
                    ct);

            LogAttachmentResult(digestData.Digest, digestData.EolDate.Value, result, dateMismatchHandling);

            if (result is LifecycleMetadataAttachmentResult.Attached attached)
            {
                createdAnnotationDigests.Add(attached.Artifact.Referrer.Digest);
            }
        });

        logger.LogInformation(
            "Created {CreatedCount} lifecycle artifact(s) for {EolCount} requested digest(s).",
            createdAnnotationDigests.Count,
            eolDigests.Count);

        return [.. createdAnnotationDigests];
    }

    private void LogAttachmentResult(
        string digest,
        DateOnly eolDate,
        LifecycleMetadataAttachmentResult result,
        DateMismatchHandling dateMismatchHandling)
    {
        switch (result)
        {
            case LifecycleMetadataAttachmentResult.Attached:
                logger.LogInformation(
                    "Attached lifecycle metadata to '{Digest}' with EOL date '{EolDate}'.",
                    digest,
                    eolDate);
                break;

            case LifecycleMetadataAttachmentResult.AlreadyMatching:
                logger.LogDebug(
                    "Skipping '{Digest}' because its existing EOL date matches '{EolDate}'.",
                    digest,
                    eolDate);
                break;

            case LifecycleMetadataAttachmentResult.ConflictSkipped conflict
                when dateMismatchHandling == DateMismatchHandling.Ignore:
                logger.LogDebug(
                    "Skipping '{Digest}' because it already has lifecycle metadata with EOL date '{ExistingEolDate}'.",
                    digest,
                    conflict.ExistingArtifact.EndOfLifeDate);
                break;

            case LifecycleMetadataAttachmentResult.ConflictSkipped conflict:
                logger.LogWarning(
                    "Skipping '{Digest}' because its existing EOL date '{ExistingEolDate}' conflicts with requested"
                        + " date '{EolDate}'.",
                    digest,
                    conflict.ExistingArtifact.EndOfLifeDate,
                    eolDate);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown lifecycle metadata attachment result '{result.GetType().Name}'.");
        }
    }

    private async Task WaitForIngestionAsync(
        IReadOnlyList<string> createdAnnotationDigests,
        bool waitForIngestion,
        ServiceConnection? marServiceConnection,
        MarIngestionOptions ingestionOptions,
        CancellationToken cancellationToken)
    {
        if (!waitForIngestion || createdAnnotationDigests.Count == 0)
        {
            return;
        }

        await ingestionReporter.ReportImageStatusesAsync(
            marServiceConnection,
            createdAnnotationDigests.Select(ToDigestInfo),
            ingestionOptions.WaitTimeout,
            ingestionOptions.RequeryDelay,
            minimumQueueTime: null,
            cancellationToken);
    }

    private static DigestInfo ToDigestInfo(string digestReference)
    {
        ImageName name = ImageName.Parse(digestReference);
        return new DigestInfo(name.Digest, name.Repo, tags: []);
    }

    /// <summary>
    /// How to handle an image whose existing lifecycle metadata has a different EOL date than requested.
    /// </summary>
    private enum DateMismatchHandling
    {
        Ignore,
        Warn,
        Fail,
    }
}
