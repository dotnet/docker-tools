// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Extensions.Options;
using Polly;


namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CleanAcrImagesCommand : Command<CleanAcrImagesOptions>
    {
        private readonly IAcrClientFactory _acrClientFactory;
        private readonly IAcrContentClientFactory _acrContentClientFactory;
        private readonly ILogger<CleanAcrImagesCommand> _logger;
        private readonly ILifecycleMetadataService _lifecycleMetadataService;
        private readonly PublishConfiguration _publishConfig;

        private const int MaxConcurrentDeleteRequestsPerRepo = 5;
        private const int ManifestBatchSize = 250;

        public CleanAcrImagesCommand(
            IAcrClientFactory acrClientFactory,
            IAcrContentClientFactory acrContentClientFactory,
            ILogger<CleanAcrImagesCommand> logger,
            ILifecycleMetadataService lifecycleMetadataService,
            IOptions<PublishConfiguration> publishConfigOptions)
        {
            _acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
            _acrContentClientFactory = acrContentClientFactory;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifecycleMetadataService = lifecycleMetadataService ?? throw new ArgumentNullException(nameof(lifecycleMetadataService));
            _publishConfig = publishConfigOptions.Value;
        }

        protected override string Description => "Removes unnecessary images from an ACR";

        public override async Task ExecuteAsync()
        {
            if (Options.ImagesToExclude.Any() && Options.Action == CleanAcrImagesAction.Delete)
            {
                throw new NotSupportedException("Excluding images is not supported when deleting repositories");
            }

            if (Options.TimeLimitMinutes is not null && Options.Action == CleanAcrImagesAction.Delete)
            {
                throw new NotSupportedException("Time-limited cleanup is not supported when deleting repositories");
            }

            Regex repoNameFilterRegex = new(ManifestFilter.GetFilterRegexPattern(Options.RepoName));

            _logger.LogInformation("FINDING IMAGES TO CLEAN");

            _logger.LogInformation($"Connecting to ACR '{Options.RegistryName}'");
            IAcrClient acrClient = CreateAcrClient(Options.RegistryName);

            _logger.LogInformation($"Querying catalog of ACR '{Options.RegistryName}'");
            IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNamesAsync();

            _logger.LogInformation("DELETING IMAGES");

            List<string> deletedRepos = new List<string>();
            List<string> deletedImages = new List<string>();
            TimeSpan? timeLimit = Options.TimeLimitMinutes is ushort timeLimitMinutes
                ? TimeSpan.FromMinutes(timeLimitMinutes)
                : null;
            using CancellationTokenSource timeLimitCancellation = CreateTimeLimitCancellation(timeLimit);

            try
            {
                await foreach (string repoName in repositoryNames
                    .Where(repoName => repoNameFilterRegex.IsMatch(repoName))
                    .WithCancellation(timeLimitCancellation.Token))
                {
                    timeLimitCancellation.Token.ThrowIfCancellationRequested();
                    ContainerRepository repo = acrClient.GetRepository(repoName);
                    Acr acr = Acr.Parse(Options.RegistryName);
                    IAcrContentClient acrContentClient = CreateAcrContentClient(acr, repo.Name);
                    await ProcessRepoAsync(
                        acrClient,
                        acrContentClient,
                        repo,
                        deletedRepos,
                        deletedImages,
                        timeLimitCancellation.Token);
                }
            }
            catch (OperationCanceledException) when (timeLimitCancellation.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Cleanup time limit of {TimeLimit} reached; stopping cleanup for registry '{RegistryName}'",
                    timeLimit.GetValueOrDefault(),
                    Options.RegistryName);
            }

            await LogSummaryAsync(acrClient, deletedRepos, deletedImages);
        }

        private async Task ProcessRepoAsync(
            IAcrClient acrClient,
            IAcrContentClient acrContentClient,
            ContainerRepository repository,
            List<string> deletedRepos,
            List<string> deletedImages,
            CancellationToken cancellationToken)
        {
            switch (Options.Action)
            {
                case CleanAcrImagesAction.PruneDangling:
                    await ProcessManifestsAsync(
                        acrClient,
                        acrContentClient,
                        deletedImages,
                        deletedRepos,
                        repository,
                        (manifest, _) => Task.FromResult(
                            !manifest.Tags.Any() && IsExpired(manifest.LastUpdatedOn, Options.Age)),
                        cancellationToken);
                    break;

                case CleanAcrImagesAction.PruneEol:
                    await ProcessManifestsAsync(
                        acrClient,
                        acrContentClient,
                        deletedImages,
                        deletedRepos,
                        repository,
                        async (manifest, ct) =>
                            !await IsAnnotationManifestAsync(manifest, acrContentClient)
                            && await HasExpiredEolAsync(manifest, Options.Age, ct),
                        cancellationToken);
                    break;

                case CleanAcrImagesAction.PruneAll:
                    await ProcessManifestsAsync(
                        acrClient,
                        acrContentClient,
                        deletedImages,
                        deletedRepos,
                        repository,
                        (manifest, _) => Task.FromResult(
                            IsExpired(manifest.LastUpdatedOn, Options.Age)),
                        cancellationToken);
                    break;

                case CleanAcrImagesAction.Delete:
                    ContainerRepositoryProperties repoProperties = repository.GetProperties().Value;
                    bool isDeleting = IsExpired(repoProperties.LastUpdatedOn, Options.Age);
                    _logger.LogInformation(
                        "Repository {RepositoryName}: CreatedOn={CreatedOn}, LastUpdatedOn={LastUpdatedOn}, ManifestCount={ManifestCount}, Deleting={Deleting}, Reason={Reason}",
                        repository.Name,
                        repoProperties.CreatedOn,
                        repoProperties.LastUpdatedOn,
                        repoProperties.ManifestCount,
                        isDeleting,
                        isDeleting
                            ? $"LastUpdatedOn is older than {Options.Age} days"
                            : $"LastUpdatedOn is within {Options.Age} days");

                    if (isDeleting)
                    {
                        await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unsupported action: {Options.Action}");
            }
        }

        private async Task LogSummaryAsync(
            IAcrClient acrClient,
            List<string> deletedRepos,
            List<string> deletedImages)
        {
            _logger.LogInformation("SUMMARY");

            _logger.LogInformation("Deleted repositories:");
            foreach (string deletedRepo in deletedRepos)
            {
                _logger.LogInformation($"\t{deletedRepo}");
            }

            _logger.LogInformation(string.Empty);

            _logger.LogInformation("Deleted images:");
            foreach (string deletedImage in deletedImages)
            {
                _logger.LogInformation($"\t{deletedImage}");
            }

            _logger.LogInformation(string.Empty);

            _logger.LogInformation("DELETED DATA");
            _logger.LogInformation($"Total images deleted: {deletedImages.Count}");
            _logger.LogInformation($"Total repos deleted: {deletedRepos.Count}");
            _logger.LogInformation(string.Empty);

            if (Options.TimeLimitMinutes is null)
            {
                _logger.LogInformation("<Querying remaining data...>");

                // Requery the catalog to get the latest info after things have been deleted
                int repositoryCount = await acrClient.GetRepositoryNamesAsync().CountAsync();
                _logger.LogInformation($"Total repos remaining: {repositoryCount}");
            }
        }

        private async Task ProcessManifestsAsync(
            IAcrClient acrClient,
            IAcrContentClient acrContentClient,
            List<string> deletedImages,
            List<string> deletedRepos,
            ContainerRepository repository,
            Func<ArtifactManifestProperties, CancellationToken, Task<bool>> canDeleteManifest,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Querying manifests for repo '{repository.Name}'");

            IAsyncEnumerable<ArtifactManifestProperties> manifests = repository.GetAllManifestPropertiesAsync();
            int manifestCount = 0;

            await foreach (IList<ArtifactManifestProperties> batch in
                manifests.Buffer(ManifestBatchSize).WithCancellation(cancellationToken))
            {
                manifestCount += batch.Count;
                ConcurrentBag<string> digestsToDelete =
                    await FindManifestsToDeleteAsync(batch, canDeleteManifest, cancellationToken);
                await DeleteManifestsAsync(acrContentClient, deletedImages, repository, digestsToDelete);
            }

            _logger.LogInformation($"Finished querying manifests for repo '{repository.Name}'. Manifest count: {manifestCount}");

            if (manifestCount == 0)
            {
                await DeleteRepositoryAsync(acrClient, deletedRepos, repository, []);
            }
        }

        private async Task<ConcurrentBag<string>> FindManifestsToDeleteAsync(
            IEnumerable<ArtifactManifestProperties> manifests,
            Func<ArtifactManifestProperties, CancellationToken, Task<bool>> canDeleteManifest,
            CancellationToken cancellationToken)
        {
            ConcurrentBag<string> digestsToDelete = [];

            await Parallel.ForEachAsync(
                manifests,
                cancellationToken,
                async (manifest, ct) =>
                {
                    if (!IsExcludedManifest(manifest) && await canDeleteManifest(manifest, ct))
                    {
                        digestsToDelete.Add(manifest.Digest);
                    }
                }
            );

            return digestsToDelete;
        }

        private bool IsExcludedManifest(ArtifactManifestProperties manifest) =>
            Options.ImagesToExclude
                .Select(exclusion => ImageName.Parse(exclusion))
                .Any(exclusion =>
                    exclusion.Repo == manifest.RepositoryName
                    && (exclusion.Digest == manifest.Digest || manifest.Tags.Contains(exclusion.Tag)));

        private async Task DeleteManifestsAsync(
            IAcrContentClient acrContentClient,
            List<string> deletedImages,
            ContainerRepository repository,
            IEnumerable<string> digests)
        {
            ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
                // Allow any number of tasks to be queued up but only allow X number of them to execute concurrently
                .AddConcurrencyLimiter(permitLimit: MaxConcurrentDeleteRequestsPerRepo, queueLimit: int.MaxValue)
                .Build();

            IEnumerable<Task> tasks =
                digests.Select(digest =>
                    pipeline.ExecuteAsync(async cancellationToken =>
                        await DeleteManifestAsync(acrContentClient, deletedImages, repository, digest))
                    .AsTask());

            await Task.WhenAll(tasks);
        }

        private async Task DeleteManifestAsync(
            IAcrContentClient acrContentClient,
            List<string> deletedImages,
            ContainerRepository repository,
            string digest)
        {
            if (!Options.IsDryRun)
            {
                await acrContentClient.DeleteManifestAsync(digest);
            }

            string imageId = $"{repository.Name}@{digest}";

            _logger.LogInformation($"Deleted image '{imageId}'");

            lock (deletedImages)
            {
                deletedImages.Add(imageId);
            }
        }

        private async Task DeleteRepositoryAsync(IAcrClient acrClient, List<string> deletedRepos, ContainerRepository repository)
        {
            IAsyncEnumerable<ArtifactManifestProperties> manifestProperties = repository.GetAllManifestPropertiesAsync();
            ArtifactManifestProperties[] allManifests = await manifestProperties.ToArrayAsync();
            await DeleteRepositoryAsync(acrClient, deletedRepos, repository, allManifests);
        }

        private async Task DeleteRepositoryAsync(
            IAcrClient acrClient,
            List<string> deletedRepos,
            ContainerRepository repository,
            ArtifactManifestProperties[] allManifests)
        {
            string[] manifestsDeleted = allManifests
                .Select(manifest => manifest.Digest)
                .ToArray();

            string[] tagsDeleted = allManifests
                .SelectMany(manifest => manifest.Tags)
                .ToArray();

            if (!Options.IsDryRun)
            {
                await acrClient.DeleteRepositoryAsync(repository.Name);
            }

            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"Deleted repository '{repository.Name}'");
            messageBuilder.AppendLine($"\tIncluded manifests:");
            foreach (string manifest in manifestsDeleted.OrderBy(manifest => manifest))
            {
                messageBuilder.AppendLine($"\t{manifest}");
            }

            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"\tIncluded tags:");
            foreach (string tag in tagsDeleted.OrderBy(tag => tag))
            {
                messageBuilder.AppendLine($"\t{tag}");
            }

            _logger.LogInformation(messageBuilder.ToString());

            lock (deletedRepos)
            {
                deletedRepos.Add(repository.Name);
            }
        }

        private static bool IsExpired(DateTimeOffset dateTime, int expirationDays) => dateTime.AddDays(expirationDays) < DateTimeOffset.Now;

        private async Task<bool> IsAnnotationManifestAsync(
            ArtifactManifestProperties manifest,
            IAcrContentClient acrContentClient)
        {
            ManifestQueryResult manifestResult = await acrContentClient.GetManifestAsync(manifest.Digest);

            // An annotation is just a referrer and referrers are indicated by the presence of a subject field.
            return manifestResult.Manifest["subject"] is not null;
        }

        private async Task<bool> HasExpiredEolAsync(
            ArtifactManifestProperties manifest,
            int eolGracePeriodDays,
            CancellationToken cancellationToken)
        {
            Manifest? lifecycleArtifactManifest = await _lifecycleMetadataService.IsDigestAnnotatedForEolAsync(
                $"{manifest.RegistryLoginServer}/{manifest.RepositoryName}@{manifest.Digest}",
                cancellationToken);

            if (lifecycleArtifactManifest?.Annotations is not null
                && lifecycleArtifactManifest.Annotations.TryGetValue(
                    LifecycleMetadataService.EndOfLifeAnnotation,
                    out string? endOfLifeValue)
                && DateTimeOffset.TryParse(endOfLifeValue, out DateTimeOffset endOfLifeDateTime))
            {
                return IsExpired(endOfLifeDateTime, eolGracePeriodDays);
            }

            return false;
        }

        /// <summary>
        /// Creates an ACR client, using the dedicated clean service connection if configured.
        /// Falls back to the default registry authentication lookup.
        /// </summary>
        private IAcrClient CreateAcrClient(string acrName) =>
            _publishConfig.CleanServiceConnection is { } svc
                ? _acrClientFactory.Create(acrName, svc)
                : _acrClientFactory.Create(acrName);

        /// <summary>
        /// Creates an ACR content client, using the dedicated clean service connection if configured.
        /// Falls back to the default registry authentication lookup.
        /// </summary>
        private IAcrContentClient CreateAcrContentClient(Acr acr, string repositoryName) =>
            _publishConfig.CleanServiceConnection is { } svc
                ? _acrContentClientFactory.Create(acr, repositoryName, svc)
                : _acrContentClientFactory.Create(acr, repositoryName);

        private static CancellationTokenSource CreateTimeLimitCancellation(TimeSpan? timeLimit)
        {
            CancellationTokenSource cancellation = new();
            if (timeLimit is TimeSpan limit)
            {
                if (limit <= TimeSpan.Zero)
                {
                    cancellation.Cancel();
                }
                else
                {
                    cancellation.CancelAfter(limit);
                }
            }

            return cancellation;
        }
    }
}
