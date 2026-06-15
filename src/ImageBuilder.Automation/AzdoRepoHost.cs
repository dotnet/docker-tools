// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// An <see cref="IRepoHost"/> for repositories hosted on Azure DevOps.
/// Branches are pushed with the git CLI; the Azure DevOps REST API is only
/// used to manage pull requests and comments. Pull requests are always
/// created from a branch in the target repository (Azure DevOps forks are not
/// supported).
/// </summary>
public sealed class AzdoRepoHost : IRepoHost
{
    private static readonly HttpClient s_httpClient = new();

    private readonly RepoHostEngine _engine;

    public AzdoRepoHost(
        AzdoRepo repo,
        GitAutomationOptions options,
        ILoggerFactory? loggerFactory = null)
        : this(repo, options, loggerFactory, s_httpClient)
    {
    }

    internal AzdoRepoHost(
        AzdoRepo repo,
        GitAutomationOptions options,
        ILoggerFactory? loggerFactory,
        HttpClient httpClient)
    {
        ILogger logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AzdoRepoHost>();
        _engine = new RepoHostEngine(
            repo,
            headRepo: repo,
            new AzdoPullRequestApi(httpClient, repo, options.Token),
            options,
            logger);
    }

    /// <inheritdoc/>
    public Task<EnsureResult> EnsurePullRequestAsync(PullRequestSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsurePullRequestAsync(spec, cancellationToken);

    /// <inheritdoc/>
    public Task<EnsureResult> EnsureBranchAsync(BranchSpec spec, CancellationToken cancellationToken = default) =>
        _engine.EnsureBranchAsync(spec, cancellationToken);
}

/// <summary>
/// Pull request operations backed by the Azure DevOps REST API. Comments are
/// posted as new threads; reading comments returns the text comments of all
/// threads (system entries are excluded).
/// </summary>
internal sealed class AzdoPullRequestApi(HttpClient httpClient, AzdoRepo repo, string token) : IPullRequestApi
{
    private const string ApiVersion = "7.1";

    private readonly string _pullRequestsApiUrl =
        $"https://dev.azure.com/{repo.Organization}/{Uri.EscapeDataString(repo.Project)}"
        + $"/_apis/git/repositories/{Uri.EscapeDataString(repo.Name)}/pullrequests";

    public async Task<PullRequestInfo?> FindOpenAsync(
        string headBranch, string targetBranch, CancellationToken cancellationToken)
    {
        string url = $"{_pullRequestsApiUrl}"
            + $"?searchCriteria.status=active"
            + $"&searchCriteria.sourceRefName={Uri.EscapeDataString($"refs/heads/{headBranch}")}"
            + $"&searchCriteria.targetRefName={Uri.EscapeDataString($"refs/heads/{targetBranch}")}"
            + $"&api-version={ApiVersion}";

        ValueList<AzdoPullRequest>? list =
            await SendAsync<ValueList<AzdoPullRequest>>(HttpMethod.Get, url, content: null, cancellationToken);
        return list?.Value is [var pullRequest, ..] ? ToInfo(pullRequest) : null;
    }

    public async Task<PullRequestInfo> CreateAsync(
        string title, string body, string headBranch, string targetBranch, CancellationToken cancellationToken)
    {
        var content = new
        {
            sourceRefName = $"refs/heads/{headBranch}",
            targetRefName = $"refs/heads/{targetBranch}",
            title,
            description = body,
        };

        AzdoPullRequest pullRequest = await SendAsync<AzdoPullRequest>(
            HttpMethod.Post, $"{_pullRequestsApiUrl}?api-version={ApiVersion}", content, cancellationToken)
            ?? throw new InvalidOperationException("Azure DevOps returned an empty create pull request response.");

        return ToInfo(pullRequest);
    }

    public Task UpdateAsync(long id, string title, string body, CancellationToken cancellationToken) =>
        SendAsync<AzdoPullRequest>(
            HttpMethod.Patch,
            $"{_pullRequestsApiUrl}/{id}?api-version={ApiVersion}",
            new { title, description = body },
            cancellationToken);

    public async Task<IReadOnlyList<string>> GetCommentsAsync(long id, CancellationToken cancellationToken)
    {
        ValueList<AzdoThread>? threads = await SendAsync<ValueList<AzdoThread>>(
            HttpMethod.Get,
            $"{_pullRequestsApiUrl}/{id}/threads?api-version={ApiVersion}",
            content: null,
            cancellationToken);

        return
        [
            .. (threads?.Value ?? [])
                .SelectMany(thread => thread.Comments ?? [])
                .Where(comment => comment.CommentType == "text" && comment.Content is not null)
                .Select(comment => comment.Content!),
        ];
    }

    public Task AddCommentAsync(long id, string comment, CancellationToken cancellationToken) =>
        SendAsync<AzdoThread>(
            HttpMethod.Post,
            $"{_pullRequestsApiUrl}/{id}/threads?api-version={ApiVersion}",
            new
            {
                comments = new[] { new { parentCommentId = 0, content = comment, commentType = "text" } },
                status = "active",
            },
            cancellationToken);

    private async Task<TResponse?> SendAsync<TResponse>(
        HttpMethod method, string url, object? content, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{token}")));

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Azure DevOps request '{method} {url}' failed with status {(int)response.StatusCode}: {error}",
                inner: null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(JsonSerializerOptions.Web, cancellationToken);
    }

    private PullRequestInfo ToInfo(AzdoPullRequest pullRequest) =>
        new(
            pullRequest.PullRequestId,
            $"https://dev.azure.com/{repo.Organization}/{Uri.EscapeDataString(repo.Project)}"
            + $"/_git/{Uri.EscapeDataString(repo.Name)}/pullrequest/{pullRequest.PullRequestId}",
            pullRequest.Title,
            pullRequest.Description ?? string.Empty);

    private sealed record ValueList<T>(List<T> Value);

    private sealed record AzdoPullRequest(int PullRequestId, string Title, string? Description);

    private sealed record AzdoThread(List<AzdoComment>? Comments);

    private sealed record AzdoComment(string? Content, string? CommentType);
}
