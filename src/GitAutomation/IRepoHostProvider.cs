// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.GitAutomation.GitHub;

namespace Microsoft.DotNet.GitAutomation;

internal interface IRepoHostProvider
{
    ValueTask<IRepoHost> OpenAsync(
        GitHubRepo targetRepo,
        GitHubRepo sourceRepo,
        CancellationToken cancellationToken);
}
