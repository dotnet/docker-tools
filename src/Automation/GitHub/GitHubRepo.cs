// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.GitHub;

/// <summary>
/// A repository hosted on GitHub.
/// </summary>
/// <param name="Owner">The user or organization that owns the repository (e.g. "dotnet").</param>
/// <param name="Name">The name of the repository (e.g. "dotnet-docker").</param>
public sealed record GitHubRepo(string Owner, string Name) : RemoteRepo
{
    public override Uri CloneUrl => new($"https://github.com/{Owner}/{Name}");

    internal override Uri GetAuthenticatedCloneUrl(string token) =>
        string.IsNullOrEmpty(token)
            ? CloneUrl
            : new Uri($"https://x-access-token:{Uri.EscapeDataString(token)}@github.com/{Owner}/{Name}");
}
