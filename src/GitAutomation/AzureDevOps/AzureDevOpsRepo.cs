// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

/// <summary>
/// Identifies an Azure DevOps Git repository.
/// </summary>
public sealed record AzureDevOpsRepo
{
    /// <summary>
    /// Creates an Azure DevOps repository identifier.
    /// </summary>
    /// <param name="organizationUri">
    /// The organization URI, such as <c>https://dev.azure.com/example/</c>.
    /// </param>
    /// <param name="project">The project name or ID.</param>
    /// <param name="name">The repository name or ID.</param>
    public AzureDevOpsRepo(Uri organizationUri, string project, string name)
    {
        ArgumentNullException.ThrowIfNull(organizationUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!organizationUri.IsAbsoluteUri || organizationUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "The Azure DevOps organization URI must be an absolute HTTPS URI.",
                nameof(organizationUri));
        }

        OrganizationUri = new Uri($"{organizationUri.AbsoluteUri.TrimEnd('/')}/");
        Project = project;
        Name = name;
    }

    /// <summary>The Azure DevOps organization URI.</summary>
    public Uri OrganizationUri { get; }

    /// <summary>The project name or ID.</summary>
    public string Project { get; }

    /// <summary>The repository name or ID.</summary>
    public string Name { get; }

    internal Uri GetCloneUrl() =>
        new Uri(OrganizationUri, $"{Escape(Project)}/_git/{Escape(Name)}");

    internal Uri GetCommitUrl(string sha) =>
        new Uri($"{GetCloneUrl().AbsoluteUri.TrimEnd('/')}/commit/{Escape(sha)}");

    internal Uri GetPullRequestUrl(int pullRequestId) =>
        new Uri($"{GetCloneUrl().AbsoluteUri.TrimEnd('/')}/pullrequest/{pullRequestId}");

    internal Uri GetApiUrl(string relativePathAndQuery = "")
    {
        string repositoryPath = $"{Escape(Project)}/_apis/git/repositories/{Escape(Name)}";

        string separator =
            string.IsNullOrEmpty(relativePathAndQuery)
            || relativePathAndQuery.StartsWith('?') ? "" : "/";

        return new(OrganizationUri, $"{repositoryPath}{separator}{relativePathAndQuery}");
    }

    internal bool IsSameRepository(AzureDevOpsRepo other) =>
        Uri.Compare(
            OrganizationUri,
            other.OrganizationUri,
            partsToCompare: UriComponents.AbsoluteUri,
            compareFormat: UriFormat.SafeUnescaped,
            comparisonType: StringComparison.OrdinalIgnoreCase) == 0
        && string.Equals(Project, other.Project, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    private static string Escape(string value) =>
        Uri.EscapeDataString(value);
}
