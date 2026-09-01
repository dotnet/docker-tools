// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

internal static class AzureDevOpsExtensions
{
    extension(CommitInfo)
    {
        public static CommitInfo FromAzureDevOpsCommit(AzureDevOpsCommit commit) =>
            new CommitInfo(commit.CommitId, commit.Author.Name, commit.Author.Email);
    }
}
