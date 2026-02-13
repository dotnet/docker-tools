// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.QueueNotification;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using WebApi = Microsoft.TeamFoundation.Build.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class QueueBuildCommand : Command<QueueBuildOptions, QueueBuildOptionsBuilder>
    {
        private readonly IVssConnectionFactory _connectionFactory;
        private readonly ILogger<QueueBuildCommand> _logger;
        private readonly INotificationService _notificationService;

        // The number of most recent builds that must have failed consecutively before skipping the queuing of another build
        public const int BuildFailureLimit = 3;

        public QueueBuildCommand(
            IVssConnectionFactory connectionFactory,
            ILogger<QueueBuildCommand> logger,
            INotificationService notificationService)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
            _notificationService = notificationService;
        }

        protected override string Description => "Queues builds to update images";

        public override async Task ExecuteAsync()
        {
            string subscriptionsJson = File.ReadAllText(Options.SubscriptionsPath);
            Subscription[] subscriptions = JsonConvert.DeserializeObject<Subscription[]>(subscriptionsJson)
                ?? throw new InvalidOperationException("Failed to deserialize subscriptions file.");
            IEnumerable<SubscriptionImagePaths> imagePathsBySubscription = GetAllSubscriptionImagePaths();

            if (imagePathsBySubscription.Any())
            {
                await Task.WhenAll(
                    imagePathsBySubscription.Select(kvp =>
                        QueueBuildForStaleImages(
                            subscriptions.FirstOrDefault(sub => sub.Id == kvp.SubscriptionId)
                                ?? throw new InvalidOperationException(
                                    $"Subscription with ID {kvp.SubscriptionId} not found."),
                            pathsToRebuild: kvp.ImagePaths
                        )
                    )
                );
            }
            else
            {
                _logger.LogInformation(
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
                .SelectMany(allImagePaths => JsonConvert.DeserializeObject<SubscriptionImagePaths[]>(allImagePaths)
                    ?? throw new InvalidOperationException("Failed to deserialize subscription image paths."))
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
                _logger.LogInformation($"All images for subscription '{subscription}' are using up-to-date base images. No rebuild necessary.");
                return;
            }

            string formattedPathsToRebuild = pathsToRebuild
                .Select(path => $"{CliHelper.FormatAlias(DockerfileFilterOptionsBuilder.PathOptionName)} '{path}'")
                .Aggregate((p1, p2) => $"{p1} {p2}");

            string parameters = "{\"" + subscription.PipelineTrigger.PathVariable + "\": \"" + formattedPathsToRebuild + "\"}";

            _logger.LogInformation($"Queueing build for subscription {subscription} with parameters {parameters}.");

            if (Options.IsDryRun)
            {
                return;
            }

            WebApi.Build? queuedBuild = null;
            Exception? exception = null;
            IEnumerable<string>? inProgressBuilds = null;
            IEnumerable<string>? recentFailedBuilds = null;

            try
            {
                (Uri baseUrl, VssCredentials credentials) = Options.AzdoOptions.GetConnectionDetails();

                using (Services.IVssConnection connection = _connectionFactory.Create(baseUrl, credentials))
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

                    inProgressBuilds = await GetInProgressBuildsAsync(client, subscription.PipelineTrigger.Id, project.Id);
                    if (!inProgressBuilds.Any())
                    {
                        (bool shouldDisallowBuild, IEnumerable<string> recentFailedBuildsLocal) =
                            await ShouldDisallowBuildDueToRecentFailuresAsync(client, subscription.PipelineTrigger.Id, project.Id);
                        recentFailedBuilds = recentFailedBuildsLocal;
                        if (shouldDisallowBuild)
                        {
                            _logger.LogInformation(
                                PipelineHelper.FormatErrorCommand("Unable to queue build due to too many recent build failures."));
                            _logger.LogInformation(PipelineHelper.SetResult(PipelineResult.SucceededWithIssues));
                        }
                        else
                        {
                            queuedBuild = await client.QueueBuildAsync(build);
                            await client.AddBuildTagAsync(project.Id, queuedBuild.Id, AzdoTags.AutoBuilder);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                await LogAndNotifyResultsAsync(
                    subscription, pathsToRebuild, queuedBuild, exception, inProgressBuilds, recentFailedBuilds);
            }
        }

        private async Task LogAndNotifyResultsAsync(
            Subscription subscription, IEnumerable<string> pathsToRebuild, WebApi.Build? queuedBuild, Exception? exception,
            IEnumerable<string>? inProgressBuilds, IEnumerable<string>? recentFailedBuilds)
        {
            StringBuilder notificationMarkdown = new();
            notificationMarkdown.AppendLine($"Subscription: {subscription}");
            notificationMarkdown.AppendLine("Paths to rebuild:");
            notificationMarkdown.AppendLine();

            foreach (string path in pathsToRebuild.OrderBy(path => path))
            {
                notificationMarkdown.AppendLine($"* `{path}`");
            }

            notificationMarkdown.AppendLine();

            string? category = null;
            if (queuedBuild is not null)
            {
                category = "Queued";
                string webLink = queuedBuild.GetWebLink();
                _logger.LogInformation($"Queued build {webLink}");
                notificationMarkdown.AppendLine($"[Build Link]({webLink})");
            }
            else if (recentFailedBuilds is not null)
            {
                category = "Failed";

                StringBuilder builder = new();
                builder.AppendLine(
                    $"Due to recent failures of the following builds, a build will not be queued again for subscription '{subscription}':");
                builder.AppendLine();
                foreach (string buildUri in recentFailedBuilds)
                {
                    builder.AppendLine($"* {buildUri}");
                }

                builder.AppendLine();
                builder.AppendLine(
                    $"Please investigate the cause of the failures, resolve the issue, and manually queue a build for the Dockerfile paths listed above. You must manually tag the build with a tag named '{AzdoTags.AutoBuilder}' in order for AutoBuilder to recognize that a successful build has occurred.");

                string message = builder.ToString();

                _logger.LogInformation(message);
                notificationMarkdown.AppendLine(message);
            }
            else if (inProgressBuilds is not null)
            {
                category = "Skipped";

                StringBuilder builder = new();
                builder.AppendLine($"The following in-progress builds were detected on the pipeline for subscription '{subscription}':");
                foreach (string buildUri in inProgressBuilds)
                {
                    builder.AppendLine(buildUri);
                }

                builder.AppendLine();
                builder.AppendLine("Queueing the build will be skipped.");

                string message = builder.ToString();

                _logger.LogInformation(message);
                notificationMarkdown.AppendLine(message);
            }
            else if (exception != null)
            {
                category = "Failed";
                notificationMarkdown.AppendLine("An exception was thrown when attempting to queue the build:");
                notificationMarkdown.AppendLine();
                notificationMarkdown.AppendLine("```");
                notificationMarkdown.AppendLine(exception.ToString());
                notificationMarkdown.AppendLine("```");
            }
            else
            {
                throw new NotSupportedException("Unknown state");
            }

            string header = $"AutoBuilder - {category}";
            notificationMarkdown.Insert(0, $"# {header}{Environment.NewLine}{Environment.NewLine}");

            // Add metadata to the issue so it can be used programmatically
            QueueInfo queueInfo = new()
            {
                BuildId = queuedBuild?.Id
            };

            notificationMarkdown.AppendLine();
            notificationMarkdown.AppendLine(NotificationHelper.FormatNotificationMetadata(queueInfo));

            if (!Options.GitOptions.GitHubAuthOptions.HasCredentials ||
                Options.GitOptions.Owner == string.Empty ||
                Options.GitOptions.Repo == string.Empty)
            {
                _logger.LogInformation(
                    "Skipping posting of notification because GitHub auth token, owner, and repo options were not provided.");
            }
            else
            {
                await _notificationService.PostAsync(
                    title: $"{header} - {subscription}", notificationMarkdown.ToString(),
                    labels: new string[]
                    {
                        NotificationLabels.AutoBuilder,
                        NotificationLabels.GetRepoLocationLabel(subscription.Manifest.Repo, subscription.Manifest.Branch)
                    }.AppendIf(NotificationLabels.Failure, () => exception is not null),
                    Options.GitOptions.Owner,
                    Options.GitOptions.Repo,
                    Options.GitOptions.GitHubAuthOptions,
                    Options.IsDryRun);
            }
        }

        private static async Task<IEnumerable<string>> GetInProgressBuildsAsync(IBuildHttpClient client, int pipelineId, Guid projectId)
        {
            IPagedList<WebApi.Build> builds = await client.GetBuildsAsync(
                projectId, definitions: new int[] { pipelineId }, statusFilter: WebApi.BuildStatus.InProgress);
            return builds.Select(build => build.GetWebLink());
        }

		private static async Task<(bool ShouldSkipBuild, IEnumerable<string> RecentFailedBuilds)> ShouldDisallowBuildDueToRecentFailuresAsync(
            IBuildHttpClient client, int pipelineId, Guid projectId)
        {
            List<WebApi.Build> autoBuilderBuilds = (await client.GetBuildsAsync(projectId, definitions: new int[] { pipelineId }))
                .Where(build => build.Tags.Contains(AzdoTags.AutoBuilder))
                .OrderByDescending(build => build.QueueTime)
                .Take(BuildFailureLimit)
                .ToList();

            if (autoBuilderBuilds.Count == BuildFailureLimit &&
                autoBuilderBuilds.All(build => build.Status == WebApi.BuildStatus.Completed && build.Result == WebApi.BuildResult.Failed))
            {
                return (true, autoBuilderBuilds.Select(build => build.GetWebLink()));
            }

            return (false, Enumerable.Empty<string>());
        }
    }
}
