// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IGitHubClientFactory))]
    internal class GitHubClientFactory : IGitHubClientFactory
    {
        public IGitHubClient GetClient(GitHubAuth gitHubAuth)
        {
            return new GitHubClient(gitHubAuth);
        }
    }
}
