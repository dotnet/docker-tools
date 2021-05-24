// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.DotNet.Git.IssueManager;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(INotificationService))]
    public class NotificationService : INotificationService
    {
        public async Task<Uri> PostAsync(string title, string description, IEnumerable<string> labels, string repoUrl, string gitHubAccessToken)
        {
            IssueManager issueManager = new(gitHubAccessToken);
            int issueId = await issueManager.CreateNewIssueAsync(repoUrl, title, description, labels: labels);
            return new Uri($"{repoUrl}/issues/{issueId}");
        }
    }
}
