// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface INotificationService
{
    Task PostAsync(
        string title,
        string description,
        IEnumerable<string> labels,
        string repoOwner,
        string repoName,
        GitHubAuthOptions gitHubAuth,
        bool isDryRun,
        IEnumerable<string>? comments = null);
}
