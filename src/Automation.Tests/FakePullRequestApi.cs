// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.Tests;

/// <summary>A pull request creation recorded by <see cref="FakePullRequestApi"/>.</summary>
internal sealed record RecordedCreate(long Id, string Title, string Body, string HeadBranch, string TargetBranch);

/// <summary>A pull request metadata update recorded by <see cref="FakePullRequestApi"/>.</summary>
internal sealed record RecordedUpdate(long Id, string Title, string Body);

/// <summary>A comment added to a pull request, recorded by <see cref="FakePullRequestApi"/>.</summary>
internal sealed record RecordedComment(long PullRequestId, string Body);

/// <summary>
/// An in-memory <see cref="IPullRequestApi"/> for tests. Lets a test seed the
/// "state of the world" (pre-existing open pull requests) and then assert on the
/// pull request operations the library performed (create, update, comment).
/// </summary>
internal sealed class FakePullRequestApi : IPullRequestApi
{
    private readonly List<StoredPullRequest> _pullRequests = [];
    private readonly List<RecordedCreate> _creates = [];
    private readonly List<RecordedUpdate> _updates = [];
    private readonly List<RecordedComment> _comments = [];
    private long _nextId = 1;

    public IReadOnlyList<RecordedCreate> Creates => _creates;

    public IReadOnlyList<RecordedUpdate> Updates => _updates;

    public IReadOnlyList<RecordedComment> Comments => _comments;

    /// <summary>Seeds a pre-existing open pull request and returns its id.</summary>
    public long SeedOpenPullRequest(
        string headBranch, string targetBranch, string title, string body, string? url = null)
    {
        long id = _nextId++;
        _pullRequests.Add(new StoredPullRequest(
            id, url ?? $"https://example.test/pull/{id}", title, body, headBranch, targetBranch));
        return id;
    }

    public Task<PullRequestInfo?> FindOpenAsync(
        string headBranch, string targetBranch, CancellationToken cancellationToken)
    {
        StoredPullRequest? match = _pullRequests.FirstOrDefault(
            pullRequest => pullRequest.HeadBranch == headBranch && pullRequest.TargetBranch == targetBranch);

        return Task.FromResult<PullRequestInfo?>(
            match is null ? null : new PullRequestInfo(match.Id, match.Url, match.Title, match.Body));
    }

    public Task<PullRequestInfo> CreateAsync(
        string title, string body, string headBranch, string targetBranch, CancellationToken cancellationToken)
    {
        long id = _nextId++;
        string url = $"https://example.test/pull/{id}";
        _pullRequests.Add(new StoredPullRequest(id, url, title, body, headBranch, targetBranch));
        _creates.Add(new RecordedCreate(id, title, body, headBranch, targetBranch));
        return Task.FromResult(new PullRequestInfo(id, url, title, body));
    }

    public Task UpdateAsync(long id, string title, string body, CancellationToken cancellationToken)
    {
        int index = _pullRequests.FindIndex(pullRequest => pullRequest.Id == id);
        if (index >= 0)
        {
            _pullRequests[index] = _pullRequests[index] with { Title = title, Body = body };
        }

        _updates.Add(new RecordedUpdate(id, title, body));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetCommentsAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(
            [.. _comments.Where(comment => comment.PullRequestId == id).Select(comment => comment.Body)]);

    public Task AddCommentAsync(long id, string comment, CancellationToken cancellationToken)
    {
        _comments.Add(new RecordedComment(id, comment));
        return Task.CompletedTask;
    }

    private sealed record StoredPullRequest(
        long Id, string Url, string Title, string Body, string HeadBranch, string TargetBranch);
}
