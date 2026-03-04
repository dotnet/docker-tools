// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Models.Oci;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.Extensions.Options;
using Polly;


namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CleanAcrImagesCommand : Command<CleanAcrImagesOptions, CleanAcrImagesOptionsBuilder>
    {
        private readonly IAcrClientFactory _acrClientFactory;
        private readonly IAcrContentClientFactory _acrContentClientFactory;
        private readonly ILogger<CleanAcrImagesCommand> _logger;
        private readonly ILifecycleMetadataService _lifecycleMetadataService;
        private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
        private readonly PublishConfiguration _publishConfig;

        private const int MaxConcurrentDeleteRequestsPerRepo = 5;

        public CleanAcrImagesCommand(
            IAcrClientFactory acrClientFactory,
            IAcrContentClientFactory acrContentClientFactory,
            ILogger<CleanAcrImagesCommand> logger,
            ILifecycleMetadataService lifecycleMetadataService,
            IRegistryCredentialsProvider registryCredentialsProvider,
            IOptions<PublishConfiguration> publishConfigOptions)
        {
            _acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
            _acrContentClientFactory = acrContentClientFactory;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lifecycleMetadataService = lifecycleMetadataService ?? throw new ArgumentNullException(nameof(lifecycleMetadataService));
            _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
            _publishConfig = publishConfigOptions.Value;
        }

        protected override string Description => "Removes unnecessary images from an ACR";

        public override async Task ExecuteAsync()
        {
            if (Options.ImagesToExclude.Any() && Options.Action == CleanAcrImagesAction.Delete)
            {
                throw new NotSupportedException("Excluding images is not supported when deleting repositories");
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

            await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
                isDryRun: false,
                async () =>
                {
                    IEnumerable<Task> cleanupTasks = await repositoryNames
                        .Where(repoName => repoNameFilterRegex.IsMatch(repoName))
                        .Select(repoName => acrClient.GetRepository(repoName))
                        .Select(repo =>
                        {
                            Acr acr = Acr.Parse(Options.RegistryName);
                            IAcrContentClient acrContentClient = CreateAcrContentClient(acr, repo.Name);
                            return ProcessRepoAsync(acrClient, acrContentClient, repo, deletedRepos, deletedImages);
                        })
                        .ToArrayAsync();

                    await Task.WhenAll(cleanupTasks);
                },
                Options.CredentialsOptions,
                registryName: Options.RegistryName);

            await LogSummaryAsync(acrClient, deletedRepos, deletedImages);
        }

        private async Task ProcessRepoAsync(
            IAcrClient acrClient, IAcrContentClient acrContentClient, ContainerRepository repository, List<string> deletedRepos, List<string> deletedImages)
        {
            switch (Options.Action)
            {
                case CleanAcrImagesAction.PruneDangling:
                    await ProcessManifestsAsync(acrClient, acrContentClient, deletedImages, deletedRepos, repository,
                        manifest => Task.FromResult(!manifest.Tags.Any() && IsExpired(manifest.LastUpdatedOn, Options.Age)));
                    break;
                case CleanAcrImagesAction.PruneEol:
                    await ProcessManifestsAsync(acrClient, acrContentClient, deletedImages, deletedRepos, repository,
                        async manifest => !(await IsAnnotationManifestAsync(manifest, acrContentClient)) && HasExpiredEol(manifest, Options.Age));
                    break;
                case CleanAcrImagesAction.PruneAll:
                    await ProcessManifestsAsync(acrClient, acrContentClient, deletedImages, deletedRepos, repository,
                        manifest => Task.FromResult(IsExpired(manifest.LastUpdatedOn, Options.Age)));
                    break;
                case CleanAcrImagesAction.Delete:
                    if (IsExpired(repository.GetProperties().Value.LastUpdatedOn, Options.Age))
                    {
                        await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Unsupported action: {Options.Action}");
            }
        }

        private async Task LogSummaryAsync(IAcrClient acrClient, List<string> deletedRepos, List<string> deletedImages)
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

            _logger.LogInformation("<Querying remaining data...>");

            // Requery the catalog to get the latest info after things have been deleted
            IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNamesAsync();

            _logger.LogInformation($"Total repos remaining: {await repositoryNames.CountAsync()}");

        }

        private async Task ProcessManifestsAsync(
            IAcrClient acrClient, IAcrContentClient acrContentClient, List<string> deletedImages, List<string> deletedRepos, ContainerRepository repository,
            Func<ArtifactManifestProperties, Task<bool>> canDeleteManifest)
        {
            _logger.LogInformation($"Querying manifests for repo '{repository.Name}'");
            IAsyncEnumerable<ArtifactManifestProperties> manifestProperties = repository.GetAllManifestPropertiesAsync();
            int manifestCount = await manifestProperties.CountAsync();
            _logger.LogInformation($"Finished querying manifests for repo '{repository.Name}'. Manifest count: {manifestCount}");

            ArtifactManifestProperties[] allManifests = await manifestProperties.ToArrayAsync();

            if (!allManifests.Any())
            {
                await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                return;
            }

            ConcurrentBag<ArtifactManifestProperties> expiredImages = [];
            await Parallel.ForEachAsync(allManifests, async (manifest, token) =>
            {
                if (!IsExcludedManifest(manifest) && await canDeleteManifest(manifest))
                {
                    expiredImages.Add(manifest);
                }
            });

            // If all the images in the repo are expired, delete the whole repo instead of
            // deleting each individual image.
            if (expiredImages.Count == manifestCount)
            {
                await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                return;
            }

            await DeleteManifestsAsync(acrContentClient, deletedImages, repository, expiredImages);
        }

        private bool IsExcludedManifest(ArtifactManifestProperties manifest) =>
            Options.ImagesToExclude
                .Select(exclusion => ImageName.Parse(exclusion))
                .Any(exclusion => exclusion.Repo == manifest.RepositoryName && (exclusion.Digest == manifest.Digest || manifest.Tags.Contains(exclusion.Tag)));

        private async Task DeleteManifestsAsync(
            IAcrContentClient acrContentClient, List<string> deletedImages, ContainerRepository repository, IEnumerable<ArtifactManifestProperties> manifests)
        {
            ResiliencePipeline pipeline = new ResiliencePipelineBuilder()
                // Allow any number of tasks to be queued up but only allow X number of them to execute concurrently
                .AddConcurrencyLimiter(permitLimit: MaxConcurrentDeleteRequestsPerRepo, queueLimit: int.MaxValue)
                .Build();

            IEnumerable<Task> tasks =
                manifests.Select(manifest =>
                    pipeline.ExecuteAsync(async cancellationToken =>
                        await DeleteManifestAsync(acrContentClient, deletedImages, repository, manifest))
                    .AsTask());

            await Task.WhenAll(tasks);
        }

        private async Task DeleteManifestAsync(
            IAcrContentClient acrContentClient, List<string> deletedImages, ContainerRepository repository, ArtifactManifestProperties manifest)
        {
            if (!Options.IsDryRun)
            {
                await acrContentClient.DeleteManifestAsync(manifest.Digest);
            }

            string imageId = $"{repository.Name}@{manifest.Digest}";

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

        private async Task<bool> IsAnnotationManifestAsync(ArtifactManifestProperties manifest, IAcrContentClient acrContentClient)
        {
            ManifestQueryResult manifestResult = await acrContentClient.GetManifestAsync(manifest.Digest);

            // An annotation is just a referrer and referrers are indicated by the presence of a subject field.
            return manifestResult.Manifest["subject"] is not null;
        }

        private bool HasExpiredEol(ArtifactManifestProperties manifest, int expirationDays)
        {
            if(_lifecycleMetadataService.IsDigestAnnotatedForEol(manifest.RegistryLoginServer + "/" + manifest.RepositoryName + "@" + manifest.Digest, _logger, isDryRun: false, out Manifest? lifecycleArtifactManifest) &&
                lifecycleArtifactManifest?.Annotations != null)
            {
                return IsExpired(DateTimeOffset.Parse(lifecycleArtifactManifest.Annotations[LifecycleMetadataService.EndOfLifeAnnotation]), expirationDays);
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
    }
}
