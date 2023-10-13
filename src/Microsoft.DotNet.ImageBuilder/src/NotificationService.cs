// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Git.IssueManager;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
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

        public async Task<Uri> PostAsync(
            string title,
            string description,
            IEnumerable<string> labels,
            string repoUrl,
            string gitHubAccessToken,
            bool isDryRun,
            IEnumerable<string>? comments = null)
        {
            IssueManager issueManager = new(gitHubAccessToken);

            int issueId = 0;
            if (!isDryRun)
            {
                issueId = await issueManager.CreateNewIssueAsync(repoUrl, title, description, labels: labels);
            }

            Uri issueUrl = new($"{repoUrl}/issues/{issueId}");

            if (comments != null)
            {
                foreach (string comment in comments)
                {
                    string _ = await issueManager.CreateNewIssueCommentAsync(repoUrl, issueId, comment);
                }
            }

            _loggerService.WriteSubheading("POSTED NOTIFICATION:");
            _loggerService.WriteMessage($"Issue URL: {issueUrl}");
            _loggerService.WriteMessage($"Title: {title}");
            _loggerService.WriteMessage($"Labels: {string.Join(", ", labels)}");
            _loggerService.WriteMessage($"Description:");
            _loggerService.WriteMessage($"====BEGIN DESCRIPTION MARKDOWN===");
            _loggerService.WriteMessage(description);
            _loggerService.WriteMessage($"====END DESCRIPTION MARKDOWN===");

            return issueUrl;
        }
    }
}
