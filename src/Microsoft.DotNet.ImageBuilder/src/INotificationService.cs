// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface INotificationService
{
    Task<NotificationPublishResult?> PostAsync(
        string title,
        string description,
        IEnumerable<string> labels,
        string repoOwner,
        string repoName,
        string gitHubAccessToken,
        bool isDryRun,
        IEnumerable<string>? comments = null);
}

public record NotificationPublishResult(Uri IssueUri, int IssueId);

public enum NotificationPublishResultType
{
    Posted,
    Skipped,
}
