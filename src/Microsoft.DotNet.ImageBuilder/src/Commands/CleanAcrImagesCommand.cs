// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Acr;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CleanAcrImagesCommand : Command<CleanAcrImagesOptions>
    {
        private readonly IAcrClientFactory acrClientFactory;
        private readonly ILoggerService loggerService;
        private Regex repoNameFilterRegex;

        [ImportingConstructor]
        public CleanAcrImagesCommand(IAcrClientFactory acrClientFactory, ILoggerService loggerService)
        {
            this.acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public override async Task ExecuteAsync()
        {
            this.repoNameFilterRegex = new Regex(ManifestFilter.GetFilterRegexPattern(Options.RepoName));

            this.loggerService.WriteHeading("FINDING IMAGES TO CLEAN");

            this.loggerService.WriteSubheading($"Connecting to ACR '{Options.RegistryName}'");
            using IAcrClient acrClient = await this.acrClientFactory.CreateAsync(
                Options.RegistryName, Options.Tenant, Options.Username, Options.Password);

            this.loggerService.WriteSubheading($"Querying catalog of ACR '{Options.RegistryName}'");
            Catalog catalog = await acrClient.GetCatalogAsync();

            this.loggerService.WriteHeading("DELETING IMAGES");

            List<string> deletedRepos = new List<string>();
            List<string> deletedImages = new List<string>();

            IEnumerable<Task> cleanupTasks = catalog.RepositoryNames
                .Where(repoName => this.repoNameFilterRegex.IsMatch(repoName))
                .Select(repoName => acrClient.GetRepositoryAsync(repoName))
                .Select(getRepoTask => ProcessRepoAsync(acrClient, getRepoTask, deletedRepos, deletedImages))
                .ToArray();

            await Task.WhenAll(cleanupTasks);

            await LogSummaryAsync(acrClient, deletedRepos, deletedImages);
        }

        private async Task ProcessRepoAsync(
            IAcrClient acrClient, Task<Repository> getRepoTask, List<string> deletedRepos, List<string> deletedImages)
        {
            Repository repository = await getRepoTask;

            switch (Options.Action)
            {
                case CleanAcrImagesAction.PruneDangling:
                    await ProcessManifestsAsync(acrClient, deletedImages, deletedRepos, repository,
                        manifest => !manifest.Tags.Any() && IsExpired(manifest.LastUpdateTime, Options.Age));
                    break;
                case CleanAcrImagesAction.PruneAll:
                    await ProcessManifestsAsync(acrClient, deletedImages, deletedRepos, repository,
                        manifest => IsExpired(manifest.LastUpdateTime, Options.Age));
                    break;
                case CleanAcrImagesAction.Delete:
                    if (IsExpired(repository.LastUpdateTime, Options.Age))
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
            this.loggerService.WriteHeading("SUMMARY");

            this.loggerService.WriteSubheading("Deleted repositories:");
            foreach (string deletedRepo in deletedRepos)
            {
                this.loggerService.WriteMessage($"\t{deletedRepo}");
            }

            this.loggerService.WriteMessage();

            this.loggerService.WriteSubheading("Deleted images:");
            foreach (string deletedImage in deletedImages)
            {
                this.loggerService.WriteMessage($"\t{deletedImage}");
            }

            this.loggerService.WriteMessage();

            this.loggerService.WriteSubheading("DELETED DATA");
            this.loggerService.WriteMessage($"Total images deleted: {deletedImages.Count}");
            this.loggerService.WriteMessage($"Total repos deleted: {deletedRepos.Count}");
            this.loggerService.WriteMessage();

            this.loggerService.WriteMessage("<Querying remaining data...>");

            // Requery the catalog to get the latest info after things have been deleted
            Catalog catalog = await acrClient.GetCatalogAsync();

            this.loggerService.WriteSubheading($"Total repos remaining: {catalog.RepositoryNames.Count}");

        }

        private async Task ProcessManifestsAsync(
            IAcrClient acrClient, List<string> deletedImages, List<string> deletedRepos, Repository repository,
            Func<ManifestAttributes, bool> canDeleteManifest)
        {
            this.loggerService.WriteMessage($"Querying manifests for repo '{repository.Name}'");
            RepositoryManifests repoManifests = await acrClient.GetRepositoryManifestsAsync(repository.Name);
            this.loggerService.WriteMessage($"Finished querying manifests for repo '{repository.Name}'. Manifest count: {repoManifests.Manifests.Count}");

            if (!repoManifests.Manifests.Any())
            {
                await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                return;
            }

            ManifestAttributes[] expiredTestImages = repoManifests.Manifests
                .Where(manifest => canDeleteManifest(manifest))
                .ToArray();

            // If all the images in the repo are expired, delete the whole repo instead of 
            // deleting each individual image.
            if (expiredTestImages.Length == repoManifests.Manifests.Count)
            {
                await DeleteRepositoryAsync(acrClient, deletedRepos, repository);
                return;
            }

            await DeleteManifestsAsync(acrClient, deletedImages, repository, expiredTestImages);
        }

        private async Task DeleteManifestsAsync(
            IAcrClient acrClient, List<string> deletedImages, Repository repository, IEnumerable<ManifestAttributes> manifests)
        {
            List<Task> tasks = new List<Task>();
            foreach (ManifestAttributes manifest in manifests)
            {
                tasks.Add(DeleteManifestAsync(acrClient, deletedImages, repository, manifest));
            }

            await Task.WhenAll(tasks);
        }

        private async Task DeleteManifestAsync(
            IAcrClient acrClient, List<string> deletedImages, Repository repository, ManifestAttributes manifest)
        {
            if (!Options.IsDryRun)
            {
                await acrClient.DeleteManifestAsync(repository.Name, manifest.Digest);
            }

            string imageId = $"{repository.Name}@{manifest.Digest}";

            this.loggerService.WriteMessage($"Deleted image '{imageId}'");

            lock (deletedImages)
            {
                deletedImages.Add(imageId);
            }
        }

        private async Task DeleteRepositoryAsync(IAcrClient acrClient, List<string> deletedRepos, Repository repository)
        {
            string[] manifestsDeleted;
            string[] tagsDeleted;

            ManifestAttributes[] manifests = (await acrClient.GetRepositoryManifestsAsync(repository.Name)).Manifests.ToArray();

            if (!Options.IsDryRun)
            {
                DeleteRepositoryResponse deleteResponse =
                    await acrClient.DeleteRepositoryAsync(repository.Name);
                manifestsDeleted = deleteResponse.ManifestsDeleted;
                tagsDeleted = deleteResponse.TagsDeleted;
            }
            else
            {
                manifestsDeleted = manifests
                    .Select(manifest => manifest.Digest)
                    .ToArray();

                tagsDeleted = manifests
                    .SelectMany(manifest => manifest.Tags)
                    .ToArray();
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

            this.loggerService.WriteMessage(messageBuilder.ToString());

            lock (deletedRepos)
            {
                deletedRepos.Add(repository.Name);
            }
        }

        private bool IsExpired(DateTime dateTime, int expirationDays) => dateTime.AddDays(expirationDays) < DateTime.Now;
    }
}
