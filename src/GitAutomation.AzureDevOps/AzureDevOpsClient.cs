// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Http.Json;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

/// <summary>Calls the Azure DevOps Git HTTP API.</summary>
/// <remarks>The caller owns <paramref name="httpClient"/>.</remarks>
/// <param name="httpClient">The configured HTTP client.</param>
public sealed class AzureDevOpsClient(HttpClient httpClient) : IDisposable
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    // Whether or not this client constructed its own HttpClient or if one was
    // provided by the caller. Used to determine whether the HttpClient should
    // be disposed or not.
    private readonly bool _ownsHttpClient;

    /// <summary>Creates a client for one Azure DevOps organization and project.</summary>
    /// <param name="organization">Azure DevOps organization name.</param>
    /// <param name="project">Azure DevOps project name or ID.</param>
    /// <param name="accessToken">Azure DevOps access token.</param>
    public AzureDevOpsClient(string organization, string project, string accessToken)
        : this(CreateHttpClient(organization, project, accessToken))
    {
        // This is a convenience constructor that allows the client to be used
        // without a DI container and without a ton of boilerplate code.
        // We create our own HttpClient, so we must dispose it when we're done.
        // If the caller provides their own HttpClient, they are responsible
        // for disposing it.
        _ownsHttpClient = true;
    }

    /// <summary>Gets a repository by name or ID.</summary>
    /// <param name="repository">The repository name or ID.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public async Task<AzureDevOpsRepository> GetRepositoryAsync(string repository, CancellationToken cancellationToken)
    {
        string uri = new RequestUriBuilder(repository).Build();

        return await _httpClient.GetFromJsonAsync(
            uri,
            JsonContext.Default.AzureDevOpsRepository,
            cancellationToken)
                ?? throw new InvalidOperationException("Azure DevOps returned null for a repository response.");
    }

    /// <summary>Lists active pull requests from a source ref.</summary>
    /// <param name="repository">The repository name or ID.</param>
    /// <param name="sourceRefName">The full source ref name.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public async Task<AzureDevOpsPullRequest[]> ListActivePullRequestsAsync(
        string repository,
        string sourceRefName,
        CancellationToken cancellationToken)
    {
        string uri = new RequestUriBuilder(repository)
            .AppendPath("pullrequests")
            .AddQueryParameter("searchCriteria.status", "active")
            .AddQueryParameter("searchCriteria.sourceRefName", sourceRefName)
            .Build();

        ArrayResponse<AzureDevOpsPullRequest> response = await _httpClient.GetFromJsonAsync(
            uri,
            JsonContext.Default.ArrayResponseAzureDevOpsPullRequest,
            cancellationToken)
                ?? throw new InvalidOperationException("Azure DevOps returned null for a pull request list response.");

        return response.Value;
    }

    /// <summary>Gets a pull request by ID.</summary>
    /// <param name="repository">The repository name or ID.</param>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public async Task<AzureDevOpsPullRequest> GetPullRequestAsync(
        string repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        string uri = new RequestUriBuilder(repository)
            .AppendPath("pullrequests")
            .AppendPath(pullRequestId)
            .Build();

        return await _httpClient.GetFromJsonAsync(uri, JsonContext.Default.AzureDevOpsPullRequest, cancellationToken)
            ?? throw new InvalidOperationException("Azure DevOps returned null for a pull request response.");
    }

    /// <summary>Gets all commits in a pull request.</summary>
    /// <param name="repository">The repository name or ID.</param>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public IAsyncEnumerable<AzureDevOpsCommit> GetPullRequestCommits(
        string repository,
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        return _httpClient.GetAllPages(
            getPageUrl: continuationToken =>
            {
                RequestUriBuilder uri = new RequestUriBuilder(repository)
                    .AppendPath("pullrequests")
                    .AppendPath(pullRequestId)
                    .AppendPath("commits");

                if (continuationToken is not null)
                    uri.AddQueryParameter("continuationToken", continuationToken);

                return uri.Build();
            },
            JsonContext.Default.ArrayResponseAzureDevOpsCommit,
            cancellationToken);
    }

    /// <summary>Gets a commit by ID.</summary>
    /// <param name="repository">The repository name or ID.</param>
    /// <param name="commitId">The commit ID.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public async Task<AzureDevOpsCommit> GetCommitAsync(
        string repository,
        string commitId,
        CancellationToken cancellationToken)
    {
        string uri = new RequestUriBuilder(repository)
            .AppendPath("commits")
            .AppendPath(commitId)
            .Build();

        return await _httpClient.GetFromJsonAsync(uri, JsonContext.Default.AzureDevOpsCommit, cancellationToken)
            ?? throw new InvalidOperationException("Azure DevOps returned null for a commit response.");
    }

    /// <summary>Creates a pull request.</summary>
    /// <param name="repository">The repository name or ID.</param>
    /// <param name="pullRequest">The pull request to create.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public async Task<AzureDevOpsPullRequest> CreatePullRequestAsync(
        string repository,
        AzureDevOpsCreatePullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        string uri = new RequestUriBuilder(repository)
            .AppendPath("pullrequests")
            .Build();

        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            uri,
            pullRequest,
            JsonContext.Default.AzureDevOpsCreatePullRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(JsonContext.Default.AzureDevOpsPullRequest, cancellationToken)
            ?? throw new InvalidOperationException("Azure DevOps returned null for a pull request response.");
    }

    /// <summary>Updates a pull request.</summary>
    /// <param name="repository">The repository name or ID.</param>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <param name="pullRequest">The pull request changes.</param>
    /// <param name="cancellationToken">Stops the request.</param>
    public async Task<AzureDevOpsPullRequest> UpdatePullRequestAsync(
        string repository,
        int pullRequestId,
        AzureDevOpsUpdatePullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        string uri = new RequestUriBuilder(repository)
            .AppendPath("pullrequests")
            .AppendPath(pullRequestId)
            .Build();

        using HttpResponseMessage response = await _httpClient.PatchAsJsonAsync(
            uri,
            pullRequest,
            JsonContext.Default.AzureDevOpsUpdatePullRequest,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync(JsonContext.Default.AzureDevOpsPullRequest, cancellationToken)
            ?? throw new InvalidOperationException("Azure DevOps returned null for a pull request response.");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient(string organization, string project, string accessToken)
    {
        var authenticationHandler = new AuthenticationHandler(accessToken)
        {
            InnerHandler = new HttpClientHandler(),
        };

        return new HttpClient(authenticationHandler)
        {
            BaseAddress = UrlHelper.GetBaseAddress(organization, project),
        };
    }
}
