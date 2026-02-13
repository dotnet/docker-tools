// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder
{
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IOctokitClientFactory _octokitClientFactory;

        public NotificationService(ILogger<NotificationService> logger, IOctokitClientFactory octokitClientFactory)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _octokitClientFactory = octokitClientFactory
                ?? throw new ArgumentNullException(nameof(octokitClientFactory));
        }

        public async Task PostAsync(
            string title,
            string description,
            IEnumerable<string> labels,
            string repoOwner,
            string repoName,
            GitHubAuthOptions gitHubAuth,
            bool isDryRun,
            IEnumerable<string>? comments = null)
        {
            IGitHubClient github = await _octokitClientFactory.CreateGitHubClientAsync(gitHubAuth);

            Issue? issue = null;
            if (!isDryRun)
            {
                var newIssue = new NewIssue(title) { Body = description };
                foreach (string label in labels)
                {
                    newIssue.Labels.Add(label);
                }

                issue = await github.Issue.Create(repoOwner, repoName, newIssue);

                if (comments != null)
                {
                    foreach (string comment in comments)
                    {
                        IssueComment postedComment =
                            await github.Issue.Comment.Create(repoOwner, repoName, issue.Number, comment);
                    }
                }
            }

            _logger.LogInformation("POSTED NOTIFICATION:");
            _logger.LogInformation($"Issue URL: {issue?.HtmlUrl ?? "SKIPPED"}");
            _logger.LogInformation($"Title: {title}");
            _logger.LogInformation($"Labels: {string.Join(", ", labels)}");
            _logger.LogInformation($"Description:");
            _logger.LogInformation($"====BEGIN DESCRIPTION MARKDOWN===");
            _logger.LogInformation(description);
            _logger.LogInformation($"====END DESCRIPTION MARKDOWN===");

            if (comments != null)
            {
                _logger.LogInformation($"====BEGIN COMMENTS MARKDOWN===");
                for (int i = 0; i < comments.Count(); i++)
                {
                    _logger.LogInformation($"====COMMENT {i + 1} MARKDOWN===");
                    _logger.LogInformation(comments.ElementAt(i));
                }
                _logger.LogInformation($"====END COMMENTS MARKDOWN===");
            }

            if (issue is null)
            {
                return;
            }

            // Immediately close issues which aren't failures, since open issues should represent actionable items
            if (!labels.Where(l => l.Contains(Commands.NotificationLabels.Failure)).Any())
            {
                _logger.LogInformation("No failure label found in the notification labels.");
                await github.Issue.Update(repoOwner, repoName, issue.Number, new IssueUpdate { State = ItemState.Closed });
            }
        }
    }
}
