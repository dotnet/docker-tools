// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

internal sealed class AzureDevOpsRepoHostProvider(
    IAzureDevOpsAccessProvider accessProvider,
    ILoggerFactory loggerFactory,
    Git git) : IRepoHostProvider<AzureDevOpsRepo>
{
    public async ValueTask<IRepoHost> OpenAsync(
        AzureDevOpsRepo targetRepo,
        AzureDevOpsRepo sourceRepo,
        CancellationToken cancellationToken)
    {
        bool orgsHaveDifferentUris =
            Uri.Compare(
                targetRepo.OrganizationUri,
                sourceRepo.OrganizationUri,
                partsToCompare: UriComponents.AbsoluteUri,
                compareFormat: UriFormat.SafeUnescaped,
                comparisonType: StringComparison.OrdinalIgnoreCase) != 0;

        if (orgsHaveDifferentUris)
            throw new ArgumentException("Azure DevOps pull requests cannot span organizations.", nameof(sourceRepo));

        AzureDevOpsAccess access = await accessProvider.GetAccessAsync(targetRepo, cancellationToken);
        return new AzureDevOpsRepoHost(targetRepo, sourceRepo, access, loggerFactory, git);
    }
}

internal sealed class AzureDevOpsRepoHost(
    AzureDevOpsRepo targetRepo,
    AzureDevOpsRepo sourceRepo,
    AzureDevOpsAccess access,
    ILoggerFactory loggerFactory,
    Git git) : IRepoHost
{
    private const string ApiVersion = "7.1";
    private const string RefPrefix = "refs/heads/";

    private readonly ILogger<AzureDevOpsRepoHost> _logger = loggerFactory.CreateLogger<AzureDevOpsRepoHost>();
    private string? _sourceRepositoryId;

    public AutomationIdentity Identity => access.Identity;

    public Task<GitWorkspace> CloneAsync(RepositoryRole repository, string branch, CancellationToken cancellationToken)
    {
        Uri cloneUrl = GetRepository(repository).GetCloneUrl();
        string authorization = access.Authorization.ToString();

        _logger.LogInformation("Cloning {Url} branch '{Branch}'.", cloneUrl, branch);

        return GitWorkspace.CloneAsync(
            git,
            _logger,
            cloneUrl,
            secret: authorization,
            authenticationArguments: GitHttpAuthentication.GetArguments(cloneUrl),
            authenticationEnvironmentVariables: GitHttpAuthentication.GetEnvironmentVariables(authorization),
            branch,
            Identity.AuthorName,
            Identity.AuthorEmail,
            cancellationToken);
    }

    public async Task<ExistingPullRequest?> GetPullRequest(string key, CancellationToken cancellationToken)
    {
        string sourceRef = GetRefName(key);
        List<string> query =
        [
            $"searchCriteria.sourceRefName={Uri.EscapeDataString(sourceRef)}",
            "searchCriteria.status=active",
            "$top=2",
            $"api-version={ApiVersion}",
        ];

        query.Insert(
            1,
            $"searchCriteria.sourceRepositoryId={Uri.EscapeDataString(await GetSourceRepositoryIdAsync(cancellationToken))}");

        PullRequestListResponse matches = await SendAsync(
            HttpMethod.Get,
            targetRepo.GetApiUrl($"pullrequests?{string.Join('&', query)}"),
            AzureDevOpsJsonContext.Default.PullRequestListResponse,
            content: null,
            cancellationToken);

        if (matches.Value.Length == 0)
        {
            _logger.LogDebug(
                "No active pull request with source ref '{SourceRef}' in {Project}/{Repository}.",
                sourceRef,
                targetRepo.Project,
                targetRepo.Name);
            return null;
        }

        if (matches.Value.Length > 1)
        {
            throw new InvalidOperationException(
                $"Expected at most one active pull request with source ref '{sourceRef}' "
                    + $"in {targetRepo.Project}/{targetRepo.Name}, but found {matches.Value.Length}.");
        }

        int pullRequestId = RequirePositive(matches.Value[0].PullRequestId, "pullRequestId");
        PullRequestResponse pullRequest = await SendAsync(
            HttpMethod.Get,
            GetPullRequestApiUrl(pullRequestId),
            AzureDevOpsJsonContext.Default.PullRequestResponse,
            content: null,
            cancellationToken);

        string sourceCommit = await GetBranchHeadAsync(key, cancellationToken);

        CommitResponse headCommit = await SendAsync(
            HttpMethod.Get,
            sourceRepo.GetApiUrl($"commits/{Uri.EscapeDataString(sourceCommit)}?api-version={ApiVersion}"),
            AzureDevOpsJsonContext.Default.CommitResponse,
            content: null,
            cancellationToken);

        IReadOnlyList<CommitInfo> commits = await GetPullRequestCommitsAsync(pullRequestId, cancellationToken);
        Uri url = targetRepo.GetPullRequestUrl(pullRequestId);

        PullRequestState state = new(
            key,
            Require(pullRequest.Title, "title"),
            pullRequest.Description ?? string.Empty,
            GetBranchName(Require(pullRequest.TargetRefName, "targetRefName")),
            Require(headCommit.TreeId, "treeId"));

        _logger.LogDebug(
            "Found active pull request #{Number} with source ref '{SourceRef}' ({CommitCount} commit(s)).",
            pullRequestId,
            sourceRef,
            commits.Count);

        return new(state, pullRequestId, url, commits);
    }

    public async Task<IReadOnlyList<IOperationResult>> ExecuteAsync(
        IEnumerable<IOperation> operations,
        CancellationToken cancellationToken)
    {
        List<IOperationResult> results = [];

        foreach (IOperation operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IOperationResult result = operation switch
            {
                PushCommitsOperation push => await PushAsync(push, cancellationToken),
                CreatePullRequestOperation create => await CreatePullRequestAsync(create, cancellationToken),
                UpdateTitleOperation updateTitle => await UpdateTitleAsync(updateTitle, cancellationToken),
                UpdateBodyOperation updateBody => await UpdateBodyAsync(updateBody, cancellationToken),
                UpdateBaseBranchOperation updateBase => await UpdateBaseBranchAsync(updateBase, cancellationToken),
                _ => throw new InvalidOperationException($"Unknown operation type '{operation.GetType()}'."),
            };

            results.Add(result);
        }

        return results;
    }

    private async Task<CommitsPushed> PushAsync(PushCommitsOperation operation, CancellationToken cancellationToken)
    {
        string authorization = access.Authorization.ToString();
        IReadOnlyDictionary<string, string> authenticationEnvironmentVariables =
            GitHttpAuthentication.GetEnvironmentVariables(authorization);
        Uri sourceUrl = sourceRepo.GetCloneUrl();
        string[] authenticationArguments = GitHttpAuthentication.GetArguments(sourceUrl);
        string branch = operation.SourceBranch;
        string directory = operation.WorkspaceDirectory;
        string remoteRef = GetRefName(branch);

        string lsRemote = await git.RunAsync(
            authorization,
            authenticationEnvironmentVariables,
            directory,
            cancellationToken,
            [.. authenticationArguments, "ls-remote", "--heads", sourceUrl.AbsoluteUri, remoteRef]);
        string? existingLine = lsRemote
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.EndsWith($"\t{remoteRef}", StringComparison.Ordinal));

        string fromSha = existingLine is null ? string.Empty : existingLine.Split('\t')[0];
        string toSha = await git.RunAsync(secret: null, directory, cancellationToken, "rev-parse", "HEAD");
        string[] pushArguments = operation.ForcePush
            ? [.. authenticationArguments, "push", "--force", sourceUrl.AbsoluteUri, $"HEAD:{remoteRef}"]
            : [.. authenticationArguments, "push", sourceUrl.AbsoluteUri, $"HEAD:{remoteRef}"];

        _logger.LogInformation(
            "Pushing commit {ToSha} to branch '{Branch}' in {Project}/{Repository}{Force}.",
            toSha,
            branch,
            sourceRepo.Project,
            sourceRepo.Name,
            operation.ForcePush ? " (force)" : string.Empty);

        await git.RunAsync(
            authorization,
            authenticationEnvironmentVariables,
            directory,
            cancellationToken,
            pushArguments);

        Uri commitUrl = sourceRepo.GetCommitUrl(toSha);
        return new(branch, fromSha, toSha, commitUrl);
    }

    private async Task<PullRequestCreated> CreatePullRequestAsync(
        CreatePullRequestOperation operation,
        CancellationToken cancellationToken)
    {
        ForkSourceRequest? forkSource = null;
        if (!sourceRepo.IsSameRepository(targetRepo))
        {
            forkSource = new(
                GetRefName(operation.SourceBranch),
                new(await GetSourceRepositoryIdAsync(cancellationToken)));
        }

        CreatePullRequestRequest request = new(
            GetRefName(operation.SourceBranch),
            GetRefName(operation.TargetBranch),
            operation.Title,
            operation.Body,
            forkSource);

        _logger.LogInformation(
            "Creating pull request '{Title}' from '{Source}' into '{Target}' in {Project}/{Repository}.",
            operation.Title,
            request.SourceRefName,
            request.TargetRefName,
            targetRepo.Project,
            targetRepo.Name);

        using JsonContent content = JsonContent.Create(
            request,
            AzureDevOpsJsonContext.Default.CreatePullRequestRequest);
        PullRequestResponse created = await SendAsync(
            HttpMethod.Post,
            targetRepo.GetApiUrl($"pullrequests?api-version={ApiVersion}"),
            AzureDevOpsJsonContext.Default.PullRequestResponse,
            content,
            cancellationToken);

        int pullRequestId = RequirePositive(created.PullRequestId, "pullRequestId");
        Uri url = targetRepo.GetPullRequestUrl(pullRequestId);
        _logger.LogInformation("Created pull request #{Number}: {Url}.", pullRequestId, url);
        return new(pullRequestId, url);
    }

    private async Task<TitleUpdated> UpdateTitleAsync(
        UpdateTitleOperation operation,
        CancellationToken cancellationToken)
    {
        await UpdatePullRequestAsync(
            operation.Number,
            new UpdatePullRequestRequest { Title = operation.Title },
            cancellationToken);
        return new(operation.Number, operation.Title);
    }

    private async Task<BodyUpdated> UpdateBodyAsync(UpdateBodyOperation operation, CancellationToken cancellationToken)
    {
        await UpdatePullRequestAsync(
            operation.Number,
            new UpdatePullRequestRequest { Description = operation.Body },
            cancellationToken);
        return new(operation.Number, operation.Body);
    }

    private async Task<BaseBranchUpdated> UpdateBaseBranchAsync(
        UpdateBaseBranchOperation operation,
        CancellationToken cancellationToken)
    {
        await UpdatePullRequestAsync(
            operation.Number,
            new UpdatePullRequestRequest { TargetRefName = GetRefName(operation.TargetBranch) },
            cancellationToken);
        return new(operation.Number, operation.TargetBranch);
    }

    private async Task UpdatePullRequestAsync(
        int pullRequestId,
        UpdatePullRequestRequest update,
        CancellationToken cancellationToken)
    {
        using JsonContent content = JsonContent.Create(update, AzureDevOpsJsonContext.Default.UpdatePullRequestRequest);
        await SendAsync(
            HttpMethod.Patch,
            GetPullRequestApiUrl(pullRequestId),
            AzureDevOpsJsonContext.Default.PullRequestResponse,
            content,
            cancellationToken);
    }

    private async Task<IReadOnlyList<CommitInfo>> GetPullRequestCommitsAsync(
        int pullRequestId,
        CancellationToken cancellationToken)
    {
        List<CommitInfo> commits = [];
        string? continuationToken = null;

        do
        {
            string continuationQuery = continuationToken is null
                ? string.Empty
                : $"&continuationToken={Uri.EscapeDataString(continuationToken)}";
            Uri uri = targetRepo.GetApiUrl(
                $"pullRequests/{pullRequestId}/commits?$top=1000&api-version={ApiVersion}{continuationQuery}");

            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, uri);
            using HttpResponseMessage response = await access.Client.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);

            CommitListResponse page =
                await response.Content.ReadFromJsonAsync(
                    AzureDevOpsJsonContext.Default.CommitListResponse,
                    cancellationToken)
                ?? throw new InvalidOperationException($"Azure DevOps returned an empty response for '{uri}'.");

            commits.AddRange(
                page.Value.Select(commit => new CommitInfo(
                    Require(commit.CommitId, "commitId"),
                    Require(commit.Author?.Name, "author.name"),
                    Require(commit.Author?.Email, "author.email"))));

            string? nextContinuationToken = response.Headers.TryGetValues(
                "x-ms-continuationtoken",
                out IEnumerable<string>? values)
                ? values.SingleOrDefault()
                : null;
            continuationToken = string.IsNullOrWhiteSpace(nextContinuationToken) ? null : nextContinuationToken;
        } while (continuationToken is not null);

        return commits;
    }

    private async Task<string> GetSourceRepositoryIdAsync(CancellationToken cancellationToken)
    {
        if (_sourceRepositoryId is null)
        {
            RepositoryResponse repository = await SendAsync(
                HttpMethod.Get,
                sourceRepo.GetApiUrl($"?api-version={ApiVersion}"),
                AzureDevOpsJsonContext.Default.RepositoryResponse,
                content: null,
                cancellationToken);
            _sourceRepositoryId = Require(repository.Id, "id");
        }

        return _sourceRepositoryId;
    }

    private async Task<string> GetBranchHeadAsync(string branch, CancellationToken cancellationToken)
    {
        string refName = GetRefName(branch);
        RefListResponse refs = await SendAsync(
            HttpMethod.Get,
            sourceRepo.GetApiUrl($"refs?filter={Uri.EscapeDataString($"heads/{branch}")}&api-version={ApiVersion}"),
            AzureDevOpsJsonContext.Default.RefListResponse,
            content: null,
            cancellationToken);
        RefResponse[] exactMatches = refs
            .Value.Where(reference => string.Equals(reference.Name, refName, StringComparison.Ordinal))
            .ToArray();

        return exactMatches.Length switch
        {
            1 => Require(exactMatches[0].ObjectId, "objectId"),
            0 => throw new InvalidOperationException($"Azure DevOps source branch '{refName}' was not found."),
            _ => throw new InvalidOperationException(
                $"Azure DevOps returned multiple source branches named '{refName}'."),
        };
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        Uri uri,
        JsonTypeInfo<T> responseType,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(method, uri);
        request.Content = content;
        using HttpResponseMessage response = await access.Client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync(responseType, cancellationToken)
            ?? throw new InvalidOperationException($"Azure DevOps returned an empty response for '{uri}'.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        HttpRequestMessage request = new(method, uri);
        request.Headers.Authorization = access.Authorization;
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Azure DevOps request failed with status {(int)response.StatusCode} "
                + $"({response.ReasonPhrase}): {responseBody}",
            inner: null,
            response.StatusCode);
    }

    private Uri GetPullRequestApiUrl(int pullRequestId) =>
        targetRepo.GetApiUrl($"pullrequests/{pullRequestId}?api-version={ApiVersion}");

    private AzureDevOpsRepo GetRepository(RepositoryRole repository) =>
        repository switch
        {
            RepositoryRole.Target => targetRepo,
            RepositoryRole.Source => sourceRepo,
            _ => throw new ArgumentOutOfRangeException(nameof(repository)),
        };

    private static string GetRefName(string branch) => $"{RefPrefix}{branch}";

    private static string GetBranchName(string refName) =>
        refName.StartsWith(RefPrefix, StringComparison.Ordinal)
            ? refName[RefPrefix.Length..]
            : throw new InvalidOperationException($"Azure DevOps returned invalid branch ref '{refName}'.");

    private static string Require(string? value, string propertyName) =>
        string.IsNullOrEmpty(value)
            ? throw new InvalidOperationException(
                $"Azure DevOps response did not contain required property '{propertyName}'.")
            : value;

    private static int RequirePositive(int value, string propertyName) =>
        value > 0
            ? value
            : throw new InvalidOperationException(
                $"Azure DevOps response did not contain valid property '{propertyName}'.");
}

internal sealed record CreatePullRequestRequest(
    string SourceRefName,
    string TargetRefName,
    string Title,
    string Description,
    ForkSourceRequest? ForkSource);

internal sealed record ForkSourceRequest(string Name, RepositoryReferenceRequest Repository);

internal sealed record RepositoryReferenceRequest(string Id);

internal sealed class UpdatePullRequestRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? TargetRefName { get; init; }
}

internal sealed class PullRequestListResponse
{
    public PullRequestResponse[] Value { get; init; } = [];
}

internal sealed class PullRequestResponse
{
    public int PullRequestId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? TargetRefName { get; init; }
}

internal sealed class CommitListResponse
{
    public CommitResponse[] Value { get; init; } = [];
}

internal sealed class CommitResponse
{
    public string? CommitId { get; init; }
    public string? TreeId { get; init; }
    public CommitAuthorResponse? Author { get; init; }
}

internal sealed class CommitAuthorResponse
{
    public string? Name { get; init; }
    public string? Email { get; init; }
}

internal sealed class RepositoryResponse
{
    public string? Id { get; init; }
}

internal sealed class RefListResponse
{
    public RefResponse[] Value { get; init; } = [];
}

internal sealed class RefResponse
{
    public string? Name { get; init; }
    public string? ObjectId { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
[JsonSerializable(typeof(CreatePullRequestRequest))]
[JsonSerializable(typeof(UpdatePullRequestRequest))]
[JsonSerializable(typeof(PullRequestListResponse))]
[JsonSerializable(typeof(PullRequestResponse))]
[JsonSerializable(typeof(CommitListResponse))]
[JsonSerializable(typeof(CommitResponse))]
[JsonSerializable(typeof(RepositoryResponse))]
[JsonSerializable(typeof(RefListResponse))]
internal sealed partial class AzureDevOpsJsonContext : JsonSerializerContext;
