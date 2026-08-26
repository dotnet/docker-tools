// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation;

internal interface IRepoHost
{
    AutomationIdentity Identity { get; }

    Task<GitWorkspace> CloneAsync(RepositoryRole repository, string branch, CancellationToken cancellationToken);

    Task<ExistingPullRequest?> GetPullRequest(string key, CancellationToken cancellationToken);

    Task<IReadOnlyList<IOperationResult>> ExecuteAsync(
        IEnumerable<IOperation> operations,
        CancellationToken cancellationToken);
}

internal enum RepositoryRole
{
    Target,
    Source,
}
