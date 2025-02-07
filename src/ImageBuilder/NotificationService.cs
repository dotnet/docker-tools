// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder
{
    [Export(typeof(INotificationService))]
    public class NotificationService : INotificationService
    {
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public NotificationService(ILoggerService loggerService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public async Task PostAsync(
            string title,
            string description,
            IEnumerable<string> labels,
            string repoOwner,
            string repoName,
            string gitHubAccessToken,
            bool isDryRun,
            IEnumerable<string>? comments = null)
        {
            IGitHubClient github = OctokitClientFactory.CreateGitHubClient(new(gitHubAccessToken));

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

            _loggerService.WriteSubheading("POSTED NOTIFICATION:");
            _loggerService.WriteMessage($"Issue URL: {issue?.HtmlUrl ?? "SKIPPED"}");
            _loggerService.WriteMessage($"Title: {title}");
            _loggerService.WriteMessage($"Labels: {string.Join(", ", labels)}");
            _loggerService.WriteMessage($"Description:");
            _loggerService.WriteMessage($"====BEGIN DESCRIPTION MARKDOWN===");
            _loggerService.WriteMessage(description);
            _loggerService.WriteMessage($"====END DESCRIPTION MARKDOWN===");

            if (comments != null)
            {
                _loggerService.WriteMessage($"====BEGIN COMMENTS MARKDOWN===");
                for (int i = 0; i < comments.Count(); i++)
                {
                    _loggerService.WriteMessage($"====COMMENT {i + 1} MARKDOWN===");
                    _loggerService.WriteMessage(comments.ElementAt(i));
                }
                _loggerService.WriteMessage($"====END COMMENTS MARKDOWN===");
            }

            if (issue is null)
            {
                return;
            }

            // Immediately close issues which aren't failures, since open issues should represent actionable items
            if (!labels.Where(l => l.Contains(Commands.NotificationLabels.Failure)).Any())
            {
                _loggerService.WriteMessage("No failure label found in the notification labels.");
                await github.Issue.Update(repoOwner, repoName, issue.Number, new IssueUpdate { State = ItemState.Closed });
            }
        }
    }
}
