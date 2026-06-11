// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// Identifies a git repository hosted on a remote service.
/// </summary>
public abstract record RemoteRepo
{
    /// <summary>
    /// The HTTPS URL used to clone the repository, without any credentials.
    /// </summary>
    public abstract Uri CloneUrl { get; }

    /// <summary>
    /// The HTTPS URL used to clone the repository, with the given token
    /// embedded as a credential. Never log this URL.
    /// </summary>
    internal abstract Uri GetAuthenticatedCloneUrl(string token);
}

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

/// <summary>
/// A repository hosted on Azure DevOps.
/// </summary>
/// <param name="Organization">The Azure DevOps organization (e.g. "dnceng").</param>
/// <param name="Project">The Azure DevOps project (e.g. "internal").</param>
/// <param name="Name">The name of the repository.</param>
public sealed record AzdoRepo(string Organization, string Project, string Name) : RemoteRepo
{
    public override Uri CloneUrl => new($"https://dev.azure.com/{Organization}/{Project}/_git/{Name}");

    internal override Uri GetAuthenticatedCloneUrl(string token) =>
        string.IsNullOrEmpty(token)
            ? CloneUrl
            : new Uri($"https://azdo:{Uri.EscapeDataString(token)}@dev.azure.com/{Organization}/{Project}/_git/{Name}");
}

/// <summary>
/// A repository on the local filesystem. Intended for testing.
/// </summary>
/// <param name="Path">The absolute path to the repository.</param>
internal sealed record LocalRepo(string Path) : RemoteRepo
{
    public override Uri CloneUrl => new UriBuilder { Scheme = Uri.UriSchemeFile, Host = "", Path = Path }.Uri;

    internal override Uri GetAuthenticatedCloneUrl(string token) => CloneUrl;
}
