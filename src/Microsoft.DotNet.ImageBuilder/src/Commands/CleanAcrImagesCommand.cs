// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Acr;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CleanAcrImagesCommand : Command<CleanAcrImagesOptions>
    {
        private readonly IAcrClientFactory acrClientFactory;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public CleanAcrImagesCommand(IAcrClientFactory acrClientFactory, ILoggerService loggerService)
        {
            this.acrClientFactory = acrClientFactory ?? throw new ArgumentNullException(nameof(acrClientFactory));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public override async Task ExecuteAsync()
        {
            this.loggerService.WriteHeading("FINDING IMAGES TO CLEAN");

            this.loggerService.WriteSubheading($"Connecting to ACR '{Options.RegistryName}'");
            using IAcrClient acrClient = await this.acrClientFactory.CreateAsync(
                Options.RegistryName, Options.Tenant, Options.Username, Options.Password);

            this.loggerService.WriteSubheading($"Querying catalog of ACR '{Options.RegistryName}'");
            Catalog catalog = await acrClient.GetCatalogAsync();

            this.loggerService.WriteSubheading($"Querying repository details of ACR '{Options.RegistryName}'");
            IEnumerable<Task<Repository>> getRepositoryTasks = catalog.RepositoryNames
                .Where(repoName => !IsPublicRepo(repoName) || IsNightlyRepo(repoName))
                .Select(repoName => acrClient.GetRepositoryAsync(repoName))
                .ToArray();
            await Task.WhenAll(getRepositoryTasks);
            IEnumerable<Repository> repositories = getRepositoryTasks
                .Select(task => task.Result)
                .ToArray();

            List<string> deletedRepos = new List<string>();
            List<string> deletedImages = new List<string>();

            this.loggerService.WriteHeading("DELETING IMAGES");
            await Task.WhenAll(GetRepoProcessingTasks(acrClient, repositories, deletedRepos, deletedImages));

            LogSummary(catalog, deletedRepos, deletedImages);
        }

        private IEnumerable<Task> GetRepoProcessingTasks(
            IAcrClient acrClient, IEnumerable<Repository> repositories, List<string> deletedRepos, List<string> deletedImages)
        {
            foreach (Repository repository in repositories)
            {
                if (IsPublicRepo(repository.Name))
                {
                    yield return ProcessPublicRepoAsync(acrClient, deletedImages, repository);
                }
                else
                {
                    yield return ProcessNonPublicRepoAsync(acrClient, deletedRepos, repository);
                }
            }
        }

        private void LogSummary(Catalog catalog, List<string> deletedRepos, List<string> deletedImages)
        {
            this.loggerService.WriteHeading("SUMMARY");

            this.loggerService.WriteSubheading("Deleted repositories:");
            foreach (string deletedRepo in deletedRepos)
            {
                this.loggerService.WriteMessage($"\t{deletedRepo}");
            }

            this.loggerService.WriteMessage();

            this.loggerService.WriteSubheading("Deleted untagged images:");
            foreach (string deletedImage in deletedImages)
            {
                this.loggerService.WriteMessage($"\t{deletedImage}");
            }

            this.loggerService.WriteMessage();

            this.loggerService.WriteSubheading($"Total untagged images deleted: {deletedImages.Count}");
            this.loggerService.WriteSubheading($"Total repos deleted: {deletedRepos.Count}");
            this.loggerService.WriteSubheading($"Total repos remaining: {catalog.RepositoryNames.Length - deletedImages.Count}");
        }

        private async Task ProcessPublicRepoAsync(IAcrClient acrClient, List<string> deletedImages, Repository repository)
        {
            RepositoryManifests repoManifests = await acrClient.GetRepositoryManifests(repository.Name);

            IEnumerable<Manifest> untaggedImages = repoManifests.Manifests
                .Where(manifest => !manifest.Tags.Any() && IsExpired(manifest.LastUpdateTime, 30));

            List<Task> tasks = new List<Task>();
            foreach (Manifest untaggedImage in untaggedImages)
            {
                tasks.Add(ProcessUntaggedImageAsync(acrClient, deletedImages, repository, untaggedImage));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessUntaggedImageAsync(
            IAcrClient acrClient, List<string> deletedImages, Repository repository, Manifest untaggedImage)
        {
            if (!Options.IsDryRun)
            {
                await acrClient.DeleteManifestAsync(repository.Name, untaggedImage.Digest);
            }

            string imageId = $"{repository.Name}@{untaggedImage.Digest}";

            this.loggerService.WriteMessage($"Deleted untagged image '{imageId}'");

            lock (deletedImages)
            {
                deletedImages.Add(imageId);
            }
        }

        private async Task ProcessNonPublicRepoAsync(IAcrClient acrClient, List<string> deletedRepos, Repository repository)
        {
            if (IsExpired(repository.LastUpdateTime, 15))
            {
                if (!Options.IsDryRun)
                {
                    DeleteRepositoryResponse deleteResponse =
                        await acrClient.DeleteRepositoryAsync(repository.Name);

                    StringBuilder messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine($"Deleted repository '{repository.Name}'");
                    messageBuilder.AppendLine($"\tIncluded manifests:");
                    foreach (string manifest in deleteResponse.ManifestsDeleted)
                    {
                        messageBuilder.AppendLine($"\t{manifest}");
                    }

                    messageBuilder.AppendLine();
                    messageBuilder.AppendLine($"\tIncluded tags:");
                    foreach (string tag in deleteResponse.TagsDeleted)
                    {
                        messageBuilder.AppendLine($"\t{tag}");
                    }

                    this.loggerService.WriteMessage(messageBuilder.ToString());
                }
                else
                {
                    this.loggerService.WriteMessage($"Deleted repository '{repository.Name}'");
                }

                lock (deletedRepos)
                {
                    deletedRepos.Add(repository.Name);
                }
            }
        }

        private bool IsExpired(DateTime dateTime, int expirationDays) => dateTime.AddDays(expirationDays) < DateTime.Now;
        private bool IsPublicRepo(string repoName) => repoName.StartsWith("public/");
        private bool IsNightlyRepo(string repoName) => repoName.Contains("/core-nightly");
    }
}
