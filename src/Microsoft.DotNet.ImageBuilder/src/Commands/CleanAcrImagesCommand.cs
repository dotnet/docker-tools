﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Azure.Identity;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CleanAcrImagesCommand : Command<CleanAcrImagesOptions, CleanAcrImagesOptionsBuilder>
    {
        private readonly IContainerRegistryClientFactory _acrClientFactory;
        private readonly IContainerRegistryContentClientFactory _acrContentClientFactory;
        private readonly ILoggerService _loggerService;
        private Regex _repoNameFilterRegex;

        [ImportingConstructor]
        public CleanAcrImagesCommand(IContainerRegistryClientFactory acrClientFactory, IContainerRegistryContentClientFactory acrContentClientFactory, ILoggerService loggerService)
        {
            _acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
            _acrContentClientFactory = acrContentClientFactory;
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Removes unnecessary images from an ACR";

        public override async Task ExecuteAsync()
        {
            _repoNameFilterRegex = new Regex(ManifestFilter.GetFilterRegexPattern(Options.RepoName));

            _loggerService.WriteHeading("FINDING IMAGES TO CLEAN");

            _loggerService.WriteSubheading($"Connecting to ACR '{Options.RegistryName}'");
            IContainerRegistryClient acrClient = _acrClientFactory.Create(Options.RegistryName, new DefaultAzureCredential());

            _loggerService.WriteSubheading($"Querying catalog of ACR '{Options.RegistryName}'");
            IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNames();

            _loggerService.WriteHeading("DELETING IMAGES");

            List<string> deletedRepos = new List<string>();
            List<string> deletedImages = new List<string>();

            IEnumerable<Task> cleanupTasks = repositoryNames.ToBlockingEnumerable()
                .Where(repoName => _repoNameFilterRegex.IsMatch(repoName))
                .Select(repoName => acrClient.GetRepository(repoName))
                .Select(repo =>
                {
                    IContainerRegistryContentClient acrContentClient = _acrContentClientFactory.Create(Options.RegistryName, repo.Name, new DefaultAzureCredential());
                    return ProcessRepoAsync(acrClient, acrContentClient, repo, deletedRepos, deletedImages);
                })
                .ToArray();

            await Task.WhenAll(cleanupTasks);

            await LogSummaryAsync(acrClient, deletedRepos, deletedImages);
        }

        private async Task ProcessRepoAsync(
            IContainerRegistryClient acrClient, IContainerRegistryContentClient acrContentClient, ContainerRepository repository, List<string> deletedRepos, List<string> deletedImages)
        {
            switch (Options.Action)
            {
                case CleanAcrImagesAction.PruneDangling:
                    await ProcessManifestsAsync(acrClient, acrContentClient, deletedImages, deletedRepos, repository,
                        manifest => !manifest.Tags.Any() && IsExpired(manifest.LastUpdatedOn, Options.Age));
                    break;
                case CleanAcrImagesAction.PruneAll:
                    await ProcessManifestsAsync(acrClient, acrContentClient, deletedImages, deletedRepos, repository,
                        manifest => IsExpired(manifest.LastUpdatedOn, Options.Age));
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

        private async Task LogSummaryAsync(IContainerRegistryClient acrClient, List<string> deletedRepos, List<string> deletedImages)
        {
            _loggerService.WriteHeading("SUMMARY");

            _loggerService.WriteSubheading("Deleted repositories:");
            foreach (string deletedRepo in deletedRepos)
            {
                _loggerService.WriteMessage($"\t{deletedRepo}");
            }

            _loggerService.WriteMessage();

            _loggerService.WriteSubheading("Deleted images:");
            foreach (string deletedImage in deletedImages)
            {
                _loggerService.WriteMessage($"\t{deletedImage}");
            }

            _loggerService.WriteMessage();

            _loggerService.WriteSubheading("DELETED DATA");
            _loggerService.WriteMessage($"Total images deleted: {deletedImages.Count}");
            _loggerService.WriteMessage($"Total repos deleted: {deletedRepos.Count}");
            _loggerService.WriteMessage();

            _loggerService.WriteMessage("<Querying remaining data...>");

            // Requery the catalog to get the latest info after things have been deleted
            IAsyncEnumerable<string> repositoryNames = acrClient.GetRepositoryNames();

            _loggerService.WriteSubheading($"Total repos remaining: {repositoryNames.ToBlockingEnumerable().Count()}");

        }

        private async Task ProcessManifestsAsync(
            IContainerRegistryClient acrClient, IContainerRegistryContentClient acrContentClient, List<string> deletedImages, List<string> deletedRepos, ContainerRepository repository,
            Func<ArtifactManifestProperties, bool> canDeleteManifest)
        {
            _loggerService.WriteMessage($"Querying manifests for repo '{repository.Name}'");
            IEnumerable<ArtifactManifestProperties> manifestProperties = repository.GetAllManifestProperties();
            int manifestCount = manifestProperties.Count();
            _loggerService.WriteMessage($"Finished querying manifests for repo '{repository.Name}'. Manifest count: {manifestCount}");

            if (!manifestProperties.Any())
            {
                await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                return;
            }

            ArtifactManifestProperties[] expiredTestImages = manifestProperties
                .Where(manifest => canDeleteManifest(manifest))
                .ToArray();

            // If all the images in the repo are expired, delete the whole repo instead of 
            // deleting each individual image.
            if (expiredTestImages.Length == manifestCount)
            {
                await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                return;
            }

            await DeleteManifestsAsync(acrContentClient, deletedImages, repository, expiredTestImages);
        }

        private async Task DeleteManifestsAsync(
            IContainerRegistryContentClient acrContentClient, List<string> deletedImages, ContainerRepository repository, IEnumerable<ArtifactManifestProperties> manifests)
        {
            List<Task> tasks = new List<Task>();
            foreach (ArtifactManifestProperties manifest in manifests)
            {
                tasks.Add(DeleteManifestAsync(acrContentClient, deletedImages, repository, manifest));
            }

            await Task.WhenAll(tasks);
        }

        private async Task DeleteManifestAsync(
            IContainerRegistryContentClient acrContentClient, List<string> deletedImages, ContainerRepository repository, ArtifactManifestProperties manifest)
        {
            if (!Options.IsDryRun)
            {
                await acrContentClient.DeleteManifestAsync(manifest.Digest);
            }

            string imageId = $"{repository.Name}@{manifest.Digest}";

            _loggerService.WriteMessage($"Deleted image '{imageId}'");

            lock (deletedImages)
            {
                deletedImages.Add(imageId);
            }
        }

        private async Task DeleteRepositoryAsync(IContainerRegistryClient acrClient, List<string> deletedRepos, ContainerRepository repository)
        {
            IEnumerable<ArtifactManifestProperties> manifestProperties = repository.GetAllManifestProperties();

            string[] manifestsDeleted = manifestProperties
                .Select(manifest => manifest.Digest)
                .ToArray();

            string[] tagsDeleted = manifestProperties
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

            _loggerService.WriteMessage(messageBuilder.ToString());

            lock (deletedRepos)
            {
                deletedRepos.Add(repository.Name);
            }
        }

        private static bool IsExpired(DateTimeOffset dateTime, int expirationDays) => dateTime.AddDays(expirationDays) < DateTimeOffset.Now;
    }
}
