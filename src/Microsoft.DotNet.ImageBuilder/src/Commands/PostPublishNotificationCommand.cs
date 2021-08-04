// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.QueueNotification;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PostPublishNotificationCommand : ManifestCommand<PostPublishNotificationOptions, PostPublishNotificationOptionsBuilder>
    {
        private readonly IVssConnectionFactory _connectionFactory;
        private readonly INotificationService _notificationService;

        [ImportingConstructor]
        public PostPublishNotificationCommand(
            IVssConnectionFactory connectionFactory,
            INotificationService notificationService)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        protected override string Description => "Posts a notification about a publishing event";

        public override async Task ExecuteAsync()
        {
            StringBuilder notificationMarkdown = new();
            string buildUrl = string.Empty;
            Dictionary<string, TaskResult?> taskResults = Options.TaskNames
                .ToDictionary(name => name, name => (TaskResult?)null);
            Dictionary<string, string> buildParameters = new();
            BuildResult overallResult = BuildResult.Succeeded;
            BuildReason buildReason = BuildReason.None;
            string? correlatedQueueNotificationUrl = null;

            if (!Options.IsDryRun)
            {
                (Uri baseUrl, VssCredentials credentials) = Options.AzdoOptions.GetConnectionDetails();
                using (IVssConnection connection = _connectionFactory.Create(baseUrl, credentials))
                using (IProjectHttpClient projectHttpClient = connection.GetProjectHttpClient())
                using (IBuildHttpClient buildClient = connection.GetBuildHttpClient())
                {
                    TeamProject project = await projectHttpClient.GetProjectAsync(Options.AzdoOptions.Project);
                    TeamFoundation.Build.WebApi.Build build = await buildClient.GetBuildAsync(project.Id, Options.BuildId);
                    buildUrl = build.GetWebLink();
                    buildReason = build.Reason;

                    // Get the build's queue-time parameters
                    if (build.Parameters is not null)
                    {
                        JObject parametersJson = JsonConvert.DeserializeObject<JObject>(build.Parameters);
                        foreach (KeyValuePair<string, JToken> pair in parametersJson)
                        {
                            buildParameters.Add(pair.Key, pair.Value.ToString());
                        }
                    }

                    overallResult = await GetBuildTaskResultsAsync(taskResults, buildClient, project);
                    correlatedQueueNotificationUrl = await GetCorrelatedQueueNotificationUrlAsync();
                }
            }

            notificationMarkdown.AppendLine($"# Publish Results");
            notificationMarkdown.AppendLine();

            WriteSummaryMarkdown(notificationMarkdown, buildUrl, overallResult, buildReason, correlatedQueueNotificationUrl);
            notificationMarkdown.AppendLine();

            WriteTaskStatusesMarkdown(taskResults, notificationMarkdown);
            notificationMarkdown.AppendLine();

            WriteBuildParameters(buildParameters, notificationMarkdown);
            notificationMarkdown.AppendLine();

            WriteTagsMarkdown(notificationMarkdown);

            await _notificationService.PostAsync(
                $"Publish Result - {Options.SourceRepo}/{Options.SourceBranch}",
                notificationMarkdown.ToString(),
                new string[]
                {
                    NotificationLabels.Publish,
                    NotificationLabels.GetRepoLocationLabel(Options.SourceRepo, Options.SourceBranch)
                }.AppendIf(NotificationLabels.Failure, () => overallResult == BuildResult.Failed),
                Options.GitOptions.GetRepoUrl().ToString(),
                Options.GitOptions.AuthToken,
                Options.IsDryRun);
        }

        private async Task<string?> GetCorrelatedQueueNotificationUrlAsync()
        {
            // In the case where the publish build was queued by AutoBuilder, this finds the GitHub issue associated
            // with that queued build.

            GitHubClient gitHubClient = new(new ProductHeaderValue("dotnet"));
            Credentials token = new(Options.GitOptions.AuthToken);
            RepositoryIssueRequest issueRequest = new()
            {
                Filter = IssueFilter.All,
                Since = DateTimeOffset.Now - TimeSpan.FromDays(2)
            };
            issueRequest.Labels.Add(NotificationLabels.AutoBuilder);
            issueRequest.Labels.Add(NotificationLabels.GetRepoLocationLabel(Options.SourceRepo, Options.SourceBranch));

            gitHubClient.Credentials = token;
            IReadOnlyList<Octokit.Issue> issues = await gitHubClient.Issue.GetAllForRepository(
                Options.GitOptions.Owner, Options.GitOptions.Repo, issueRequest);

            foreach (Octokit.Issue issue in issues)
            {
                // Get the metadata embedded within the issue body, if any
                QueueInfo? queueInfo = NotificationHelper.GetNotificationMetadata<QueueInfo>(issue.Body);
                if (queueInfo?.BuildId == Options.BuildId)
                {
                    return issue.HtmlUrl;
                }
            }

            return null;
        }

        private async Task<BuildResult> GetBuildTaskResultsAsync(Dictionary<string, TaskResult?> taskResults, IBuildHttpClient buildClient, TeamProject project)
        {
            BuildResult overallResult = BuildResult.Succeeded;
            Timeline timeline = await buildClient.GetBuildTimelineAsync(project.Id, Options.BuildId);
            foreach (string task in Options.TaskNames)
            {
                TimelineRecord? record = timeline.Records.FirstOrDefault(rec => rec.Name == task);
                if (record is null)
                {
                    throw new InvalidOperationException(
                        $"Build task with name '{task}' could not be found in the build timeline.");
                }

                taskResults[task] = record.Result;

                if (record.Result is not null)
                {
                    switch (record.Result.Value)
                    {
                        case TaskResult.SucceededWithIssues:
                            if (overallResult == BuildResult.Succeeded)
                            {
                                overallResult = BuildResult.PartiallySucceeded;
                            }
                            break;
                        case TaskResult.Failed:
                            overallResult = BuildResult.Failed;
                            break;
                        case TaskResult.Canceled:
                            overallResult = BuildResult.Canceled;
                            break;
                    }
                }
            }

            return overallResult;
        }

        private void WriteSummaryMarkdown(
            StringBuilder notificationMarkdown, string buildUrl, BuildResult overallResult, BuildReason buildReason, string? correlatedQueueNotificationUrl)
        {
            notificationMarkdown.AppendLine($"## Summary");
            notificationMarkdown.AppendLine();
            notificationMarkdown.AppendLine($"**Repo** - {Options.SourceRepo}");
            notificationMarkdown.AppendLine($"**Branch** - {Options.SourceBranch}");
            notificationMarkdown.AppendLine($"**Overall Result** - {ToEmoji(overallResult)} {overallResult}");
            notificationMarkdown.AppendLine($"**Reason** - {buildReason}");
            notificationMarkdown.AppendLine($"**Build** - [{Options.BuildId}]({buildUrl})");

            if (correlatedQueueNotificationUrl is not null)
            {
                notificationMarkdown.AppendLine($"**Queued by** - [{correlatedQueueNotificationUrl}]({correlatedQueueNotificationUrl})");
            }
        }

        private static void WriteBuildParameters(Dictionary<string, string> buildParameters, StringBuilder notificationMarkdown)
        {
            if (!buildParameters.Any())
            {
                return;
            }

            notificationMarkdown.AppendLine("## Build Parameters");
            notificationMarkdown.AppendLine();
            notificationMarkdown.AppendLine("Parameter | Value");
            notificationMarkdown.AppendLine("--- | --- ");

            foreach (KeyValuePair<string, string> pair in buildParameters)
            {
                string value = pair.Value;
                if (value.Length > 0)
                {
                    value = $"`{value}`";
                }

                notificationMarkdown.AppendLine($"{pair.Key} | {value}");
            }
        }

        private void WriteTagsMarkdown(StringBuilder notificationMarkdown)
        {
            if (!File.Exists(Options.ImageInfoPath))
            {
                return;
            }
            
            ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);
            IEnumerable<ImageData> images = imageArtifactDetails.Repos.SelectMany(repo => repo.Images);
            List<string> publishedTags = new();
            foreach (ImageData image in images)
            {
                if (image.ManifestImage is not null)
{
                    publishedTags.AddRange(image.ManifestImage.SharedTags.Select(tag => tag.FullyQualifiedName));
                }
                
                publishedTags.AddRange(
                    image.Platforms
                        .SelectMany(platform => platform.PlatformInfo.Tags.Select(tag => tag.FullyQualifiedName)));

            }

            publishedTags.Sort();

            notificationMarkdown.AppendLine("## Tags");
            notificationMarkdown.AppendLine();

            foreach (string tag in publishedTags)
            {
                notificationMarkdown.AppendLine(tag);
            }
        }

        private static void WriteTaskStatusesMarkdown(Dictionary<string, TaskResult?> taskStatuses, StringBuilder notificationMarkdown)
        {
            notificationMarkdown.AppendLine("## Task Results");
            notificationMarkdown.AppendLine();
            notificationMarkdown.AppendLine("Task | Result");
            notificationMarkdown.AppendLine("--- | --- ");
            foreach (KeyValuePair<string, TaskResult?> kvp in taskStatuses)
            {
                string emoji = string.Empty;
                string status = "Not Run";

                if (kvp.Value is not null)
                {
                    status = kvp.Value.Value.ToString();
                    emoji = ToEmoji(kvp.Value.Value);
                }

                notificationMarkdown.AppendLine($"{kvp.Key} | {emoji} {status}");
            }
        }

        private static string ToEmoji(TaskResult taskResult) =>
            taskResult switch
            {
                TaskResult.Succeeded => ":heavy_check_mark:",
                TaskResult.SucceededWithIssues => ":warning:",
                TaskResult.Failed => ":exclamation:",
                TaskResult.Canceled => ":heavy_minus_sign:",
                TaskResult.Skipped => ":no_entry_sign:",
                TaskResult.Abandoned => ":heavy_multiplication_x:",
                _ => throw new NotSupportedException($"Unexpected task result value: {taskResult}"),
            };

        private static string ToEmoji(BuildResult buildResult) =>
            buildResult switch
            {
                BuildResult.Succeeded => ":heavy_check_mark:",
                BuildResult.PartiallySucceeded => ":warning:",
                BuildResult.Failed => ":exclamation:",
                BuildResult.Canceled => ":heavy_minus_sign:",
                _ => throw new NotSupportedException($"Unexpected build result value: {buildResult}"),
            };
    }
}
#nullable disable
