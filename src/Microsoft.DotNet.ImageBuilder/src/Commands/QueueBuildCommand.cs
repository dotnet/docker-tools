﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class QueueBuildCommand : Command<QueueBuildOptions>
    {
        private readonly IVssConnectionFactory connectionFactory;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public QueueBuildCommand(
            IVssConnectionFactory connectionFactory,
            ILoggerService loggerService)
        {
            this.connectionFactory = connectionFactory;
            this.loggerService = loggerService;
        }

        public override async Task ExecuteAsync()
        {
            string subscriptionsJson = File.ReadAllText(Options.SubscriptionsPath);
            Subscription[] subscriptions = JsonConvert.DeserializeObject<Subscription[]>(subscriptionsJson);
            IEnumerable<SubscriptionImagePaths> imagePathsBySubscription = GetAllSubscriptionImagePaths();

            if (imagePathsBySubscription.Any())
            {
                await Task.WhenAll(
                    imagePathsBySubscription.Select(
                        kvp => QueueBuildForStaleImages(subscriptions.First(sub => sub.Id == kvp.SubscriptionId), kvp.ImagePaths)));
            }
            else
            {
                this.loggerService.WriteMessage(
                    $"None of the subscriptions have base images that are out-of-date. No rebuild necessary.");
            }
        }

        private IEnumerable<SubscriptionImagePaths> GetAllSubscriptionImagePaths()
        {
            // This data comes from the GetStaleImagesCommand and represents a mapping of a subscription to the Dockerfile paths
            // of the images that need to be built. A given subscription may have images that are spread across Linux/Windows, AMD64/ARM
            // which means that the paths collected for that subscription were spread across multiple jobs.  Each of the items in the 
            // Enumerable here represents the data collected by one job.  We need to consolidate the paths for a given subscription since
            // they could be spread across multiple items in the Enumerable.
            return Options.AllSubscriptionImagePaths
                .SelectMany(allImagePaths => JsonConvert.DeserializeObject<SubscriptionImagePaths[]>(allImagePaths))
                .GroupBy(imagePaths => imagePaths.SubscriptionId)
                .Select(group => new SubscriptionImagePaths
                {
                    SubscriptionId = group.Key,
                    ImagePaths = group
                        .SelectMany(subscriptionImagePaths => subscriptionImagePaths.ImagePaths)
                        .ToArray()
                })
                .ToList();
        }

        private async Task QueueBuildForStaleImages(Subscription subscription, IEnumerable<string> pathsToRebuild)
        {
            if (!pathsToRebuild.Any())
            {
                this.loggerService.WriteMessage($"All images for subscription '{subscription}' are using up-to-date base images. No rebuild necessary.");
                return;
            }

            string formattedPathsToRebuild = pathsToRebuild
                .Select(path => $"{ManifestFilterOptions.FormattedPathOption} '{path}'")
                .Aggregate((p1, p2) => $"{p1} {p2}");

            string parameters = "{\"" + subscription.PipelineTrigger.PathVariable + "\": \"" + formattedPathsToRebuild + "\"}";

            this.loggerService.WriteMessage($"Queueing build for subscription {subscription} with parameters {parameters}.");

            if (Options.IsDryRun)
            {
                return;
            }

            using (IVssConnection connection = this.connectionFactory.Create(
                new Uri($"https://dev.azure.com/{Options.BuildOrganization}"),
                new VssBasicCredential(String.Empty, Options.BuildPersonalAccessToken)))
            using (IProjectHttpClient projectHttpClient = connection.GetProjectHttpClient())
            using (IBuildHttpClient client = connection.GetBuildHttpClient())
            {
                TeamProject project = await projectHttpClient.GetProjectAsync(Options.BuildProject);

                Build build = new Build
                {
                    Project = new TeamProjectReference { Id = project.Id },
                    Definition = new BuildDefinitionReference { Id = subscription.PipelineTrigger.Id },
                    SourceBranch = subscription.RepoInfo.Branch,
                    Parameters = parameters
                };

                if (await HasInProgressBuildAsync(client, subscription.PipelineTrigger.Id, project.Id))
                {
                    this.loggerService.WriteMessage(
                        $"An in-progress build was detected on the pipeline for subscription '{subscription.ToString()}'. Queueing the build will be skipped.");
                    return;
                }

                await client.QueueBuildAsync(build);
            }
        }

        private async Task<bool> HasInProgressBuildAsync(IBuildHttpClient client, int pipelineId, Guid projectId)
        {
            IPagedList<Build> builds = await client.GetBuildsAsync(
                projectId, definitions: new int[] { pipelineId }, statusFilter: BuildStatus.InProgress);
            return builds.Any();
        }
    }
}
