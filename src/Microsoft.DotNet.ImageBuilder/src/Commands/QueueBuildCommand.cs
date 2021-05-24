// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class QueueBuildCommand : Command<QueueBuildOptions, QueueBuildOptionsBuilder>
    {
        private readonly IVssConnectionFactory _connectionFactory;
        private readonly ILoggerService _loggerService;
        private readonly INotificationService _notificationService;

        [ImportingConstructor]
        public QueueBuildCommand(
            IVssConnectionFactory connectionFactory,
            ILoggerService loggerService,
            INotificationService notificationService)
        {
            _connectionFactory = connectionFactory;
            _loggerService = loggerService;
            _notificationService = notificationService;
        }

        protected override string Description => "Queues builds to update images";

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
                _loggerService.WriteMessage(
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
                _loggerService.WriteMessage($"All images for subscription '{subscription}' are using up-to-date base images. No rebuild necessary.");
                return;
            }

            string formattedPathsToRebuild = pathsToRebuild
                .Select(path => $"{CliHelper.FormatAlias(ManifestFilterOptionsBuilder.PathOptionName)} '{path}'")
                .Aggregate((p1, p2) => $"{p1} {p2}");

            string parameters = "{\"" + subscription.PipelineTrigger.PathVariable + "\": \"" + formattedPathsToRebuild + "\"}";

            _loggerService.WriteMessage($"Queueing build for subscription {subscription} with parameters {parameters}.");

            if (Options.IsDryRun)
            {
                return;
            }

            StringBuilder notificationMarkdown = new();
            notificationMarkdown.AppendLine($"Subscription: {subscription}");
            notificationMarkdown.AppendLine("Paths to rebuild:");
            notificationMarkdown.AppendLine();

            foreach (string path in pathsToRebuild.OrderBy(path => path))
            {
                notificationMarkdown.AppendLine($"* `{path}`");
            }

            notificationMarkdown.AppendLine();

            const string QueuedCategory = "Queued";
            const string SkippedCategory = "Skipped";
            const string FailureCategory = "Failure";
            string? category = null;

            try
            {
                (Uri baseUrl, VssCredentials credentials) = Options.AzdoOptions.GetConnectionDetails();

                using (IVssConnection connection = _connectionFactory.Create(baseUrl, credentials))
                using (IProjectHttpClient projectHttpClient = connection.GetProjectHttpClient())
                using (IBuildHttpClient client = connection.GetBuildHttpClient())
                {
                    TeamProject project = await projectHttpClient.GetProjectAsync(Options.AzdoOptions.Project);

                    WebApi.Build build = new()
                    {
                        Project = new TeamProjectReference { Id = project.Id },
                        Definition = new WebApi.BuildDefinitionReference { Id = subscription.PipelineTrigger.Id },
                        SourceBranch = subscription.Manifest.Branch,
                        Parameters = parameters
                    };

                    IEnumerable<string> inProgressBuilds = await GetInProgressBuildsAsync(client, subscription.PipelineTrigger.Id, project.Id);
                    if (!inProgressBuilds.Any())
                    {
                        category = QueuedCategory;
                        WebApi.Build queuedBuild = await client.QueueBuildAsync(build);
                        notificationMarkdown.AppendLine($"[Build Link]({GetWebLink(queuedBuild)})");
                    }
                    else
                    {
                        category = SkippedCategory;
                        StringBuilder builder = new();
                        builder.AppendLine($"The following in-progress builds were detected on the pipeline for subscription '{subscription}':");
                        foreach (string buildUri in inProgressBuilds)
                        {
                            builder.AppendLine(buildUri);
                        }

                        builder.AppendLine();
                        builder.AppendLine("Queueing the build will be skipped.");

                        string message = builder.ToString();

                        _loggerService.WriteMessage(message);
                        notificationMarkdown.AppendLine(message);
                    }
                }
            }
            catch (Exception ex)
            {
                category = FailureCategory;
                notificationMarkdown.AppendLine($"An exception was thrown when attempting to queue the build:");
                notificationMarkdown.AppendLine(ex.ToString());

                throw;
            }
            finally
            {
                string header = $"AutoBuilder - {category}";
                notificationMarkdown.Insert(0, $"# {header}{Environment.NewLine}{Environment.NewLine}");

                await _notificationService.PostAsync($"{header} - {subscription}", notificationMarkdown.ToString(),
                    new string[] { NotificationLabels.AutoBuilder }.AppendIf(NotificationLabels.Failure, () => category == FailureCategory),
                    $"https://github.com/{Options.GitOptions.Owner}/{Options.GitOptions.Repo}", Options.GitOptions.AuthToken);
            }
        }

        private static async Task<IEnumerable<string>> GetInProgressBuildsAsync(IBuildHttpClient client, int pipelineId, Guid projectId)
        {
            IPagedList<WebApi.Build> builds = await client.GetBuildsAsync(
                projectId, definitions: new int[] { pipelineId }, statusFilter: WebApi.BuildStatus.InProgress);
            return builds.Select(build => GetWebLink(build));
        }

        private static string GetWebLink(WebApi.Build build) =>
            ((ReferenceLink)build.Links.Links["web"]).Href;
    }
}
#nullable disable
