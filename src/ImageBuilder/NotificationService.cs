// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder;

public class NotificationService(ILogger<NotificationService> logger, IOctokitClientFactory octokitClientFactory) : INotificationService
{
    private readonly ILogger<NotificationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IOctokitClientFactory _octokitClientFactory = octokitClientFactory
            ?? throw new ArgumentNullException(nameof(octokitClientFactory));

    public async Task PostAsync(
        string title,
        string description,
        IEnumerable<string> labels,
        string repoOwner,
        string repoName,
        GitHubAuthOptions gitHubAuth,
        bool isDryRun,
        CancellationToken cancellationToken,
        IEnumerable<string>? comments = null)
    {
        IGitHubClient github = await _octokitClientFactory.CreateGitHubClientAsync(gitHubAuth, cancellationToken);

        Issue? issue = null;
        if (!isDryRun)
        {
            NewIssue newIssue = new NewIssue(title) { Body = description };
            foreach (string label in labels)
            {
                newIssue.Labels.Add(label);
            }

            issue = await github.Issue.Create(repoOwner, repoName, newIssue);

            if (comments != null)
            {
                foreach (string comment in comments)
                {
                    await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
                        .ExecuteAsync(
                            ct => github.Issue.Comment.Create(repoOwner, repoName, issue.Number, comment),
                            cancellationToken);
                }
            }
        }

        _logger.LogInformation("POSTED NOTIFICATION:");
        _logger.LogInformation("Issue URL: {IssueUrl}", issue?.HtmlUrl ?? "SKIPPED");
        _logger.LogInformation("Title: {Title}", title);
        _logger.LogInformation("Labels: {Labels}", string.Join(", ", labels));
        _logger.LogInformation($"Description:");
        _logger.LogInformation($"====BEGIN DESCRIPTION MARKDOWN===");
        _logger.LogInformation("{Description}", description);
        _logger.LogInformation($"====END DESCRIPTION MARKDOWN===");

        if (comments != null)
        {
            _logger.LogInformation($"====BEGIN COMMENTS MARKDOWN===");
            for (int i = 0; i < comments.Count(); i++)
            {
                _logger.LogInformation("====COMMENT {CommentNumber} MARKDOWN===", i + 1);
                _logger.LogInformation("{Comment}", comments.ElementAt(i));
            }

            _logger.LogInformation($"====END COMMENTS MARKDOWN===");
        }

        if (issue is null)
        {
            return;
        }

        // Immediately close issues which aren't failures, since open issues should represent actionable items.
        // Retry with backoff to handle GitHub's eventual consistency - the issue may not be fully
        // propagated across GitHub's backend services immediately after creation.
        if (!labels.Any(l => l.Contains(NotificationLabels.Failure)))
        {
            _logger.LogInformation("No failure label found in the notification labels.");
            await RetryHelper.GetWaitAndRetryPolicy<ApiException>(_logger)
                .ExecuteAsync(
                    ct => github.Issue.Update(repoOwner, repoName, issue.Number, new IssueUpdate { State = ItemState.Closed }),
                    cancellationToken);
        }
    }
}
