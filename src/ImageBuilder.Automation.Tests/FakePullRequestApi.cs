// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

internal sealed class FakePullRequest
{
    public required long Id { get; init; }

    public required string HeadBranch { get; init; }

    public required string TargetBranch { get; init; }

    public required string Url { get; init; }

    public required string Title { get; set; }

    public required string Body { get; set; }

    public ModelPullRequestState State { get; set; } = ModelPullRequestState.Open;

    public List<string> Comments { get; } = [];
}

/// <summary>
/// An in-memory <see cref="IPullRequestApi"/> standing in for a hosting
/// service's pull request API. URLs are generated with the same scheme as
/// <see cref="ModelRepoHost"/> so that equivalence tests can compare worlds
/// verbatim.
/// </summary>
internal sealed class FakePullRequestApi : IPullRequestApi
{
    public List<FakePullRequest> PullRequests { get; } = [];

    public FakePullRequest? FindOpenByHead(string headBranch) =>
        PullRequests.FirstOrDefault(pr => pr.HeadBranch == headBranch && pr.State == ModelPullRequestState.Open);

    public Task<PullRequestInfo?> FindOpenAsync(
        string headBranch, string targetBranch, CancellationToken cancellationToken)
    {
        FakePullRequest? pullRequest = PullRequests.FirstOrDefault(pr =>
            pr.HeadBranch == headBranch && pr.TargetBranch == targetBranch
            && pr.State == ModelPullRequestState.Open);
        return Task.FromResult(pullRequest is null ? null : ToInfo(pullRequest));
    }

    public Task<PullRequestInfo> CreateAsync(
        string title, string body, string headBranch, string targetBranch, CancellationToken cancellationToken)
    {
        var pullRequest = new FakePullRequest
        {
            Id = PullRequests.Count + 1,
            HeadBranch = headBranch,
            TargetBranch = targetBranch,
            Url = $"https://model.test/pr/{headBranch}/{PullRequests.Count(pr => pr.HeadBranch == headBranch)}",
            Title = title,
            Body = body,
        };
        PullRequests.Add(pullRequest);
        return Task.FromResult(ToInfo(pullRequest));
    }

    public Task UpdateAsync(long id, string title, string body, CancellationToken cancellationToken)
    {
        FakePullRequest pullRequest = Get(id);
        pullRequest.Title = title;
        pullRequest.Body = body;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetCommentsAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([.. Get(id).Comments]);

    public Task AddCommentAsync(long id, string comment, CancellationToken cancellationToken)
    {
        Get(id).Comments.Add(comment);
        return Task.CompletedTask;
    }

    private FakePullRequest Get(long id) => PullRequests.Single(pr => pr.Id == id);

    private static PullRequestInfo ToInfo(FakePullRequest pullRequest) =>
        new(pullRequest.Id, pullRequest.Url, pullRequest.Title, pullRequest.Body);
}
