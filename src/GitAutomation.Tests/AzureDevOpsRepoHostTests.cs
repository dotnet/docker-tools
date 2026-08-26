// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.DotNet.GitAutomation.AzureDevOps;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation.Tests;

[TestClass]
public sealed class AzureDevOpsRepoHostTests
{
    private static readonly AzureDevOpsRepo Repo = new(
        new Uri("https://dev.azure.com/example"),
        "project name",
        "repo name");

    [TestMethod]
    public void AzureDevOpsRepo_RejectsInsecureOrganizationUri()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new AzureDevOpsRepo(new Uri("http://example"), "project", "repo"));
    }

    [TestMethod]
    public async Task GetPullRequest_MapsAzureDevOpsState()
    {
        QueueHttpMessageHandler handler = new(
            """{"id":"11111111-1111-1111-1111-111111111111"}""",
            """
            {"value":[{"pullRequestId":42}]}
            """,
            """
            {
              "pullRequestId":42,
              "title":"Update dependencies",
              "description":"Body",
              "targetRefName":"refs/heads/main"
            }
            """,
            """
            {
              "value":[{
                "name":"refs/heads/automation/update",
                "objectId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
              }]
            }
            """,
            """
            {"treeId":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"}
            """,
            """
            {
              "value":[{
                "commitId":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "author":{"name":"Bot","email":"bot@example.com"}
              }]
            }
            """);
        AzureDevOpsRepoHost host = CreateHost(handler);

        ExistingPullRequest? pullRequest =
            await host.GetPullRequest("automation/update", CancellationToken.None);

        Assert.IsNotNull(pullRequest);
        Assert.AreEqual(42, pullRequest.Number);
        Assert.AreEqual("Update dependencies", pullRequest.Content.Title);
        Assert.AreEqual("Body", pullRequest.Content.Body);
        Assert.AreEqual("main", pullRequest.Content.TargetBranch);
        Assert.AreEqual(
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            pullRequest.Content.TreeHash);
        Assert.AreEqual(
            new Uri("https://dev.azure.com/example/project%20name/_git/repo%20name/pullrequest/42"),
            pullRequest.Url);
        Assert.AreEqual("bot@example.com", pullRequest.Commits.Single().AuthorEmail);
        Assert.IsTrue(handler.Requests.All(request =>
            request.Authorization?.StartsWith("Bearer ", StringComparison.Ordinal) is true));
        StringAssert.Contains(
            handler.Requests[1].Uri.Query,
            "searchCriteria.sourceRefName=refs%2Fheads%2Fautomation%2Fupdate");
        StringAssert.Contains(
            handler.Requests[1].Uri.Query,
            "searchCriteria.sourceRepositoryId=11111111-1111-1111-1111-111111111111");
    }

    [TestMethod]
    public async Task ExecuteAsync_UsesAzureDevOpsPullRequestFields()
    {
        QueueHttpMessageHandler handler = new(
            """
            {"pullRequestId":42}
            """,
            """{"pullRequestId":42}""",
            """{"pullRequestId":42}""",
            """{"pullRequestId":42}""");
        AzureDevOpsRepoHost host = CreateHost(handler);

        IReadOnlyList<IOperationResult> results = await host.ExecuteAsync(
            [
                new CreatePullRequestOperation(
                    "Title",
                    "Body",
                    "automation/update",
                    "main"),
                new UpdateTitleOperation(42, "New title"),
                new UpdateBodyOperation(42, "New body"),
                new UpdateBaseBranchOperation(42, "release"),
            ],
            CancellationToken.None);

        Assert.HasCount(4, results);
        Assert.AreEqual(
            new Uri("https://dev.azure.com/example/project%20name/_git/repo%20name/pullrequest/42"),
            ((PullRequestCreated)results[0]).Url);
        Assert.AreEqual(HttpMethod.Post, handler.Requests[0].Method);
        StringAssert.Contains(
            handler.Requests[0].Body,
            "\"sourceRefName\":\"refs/heads/automation/update\"");
        StringAssert.Contains(
            handler.Requests[0].Body,
            "\"targetRefName\":\"refs/heads/main\"");
        Assert.AreEqual("""{"title":"New title"}""", handler.Requests[1].Body);
        Assert.AreEqual("""{"description":"New body"}""", handler.Requests[2].Body);
        Assert.AreEqual(
            """{"targetRefName":"refs/heads/release"}""",
            handler.Requests[3].Body);
    }

    [TestMethod]
    public async Task Push_UsesAuthorizationHeaderWithoutEmbeddingTokenInUrl()
    {
        QueueHttpMessageHandler handler = new();
        RecordingProcessRunner processRunner = new();
        AzureDevOpsRepoHost host = CreateHost(handler, processRunner);

        await host.ExecuteAsync(
            [new PushCommitsOperation("workspace", "automation/update", ForcePush: false)],
            CancellationToken.None);

        Assert.HasCount(3, processRunner.Arguments);
        Assert.IsTrue(processRunner.Arguments[0].Contains(
            "--config-env=http.https://dev.azure.com/.extraHeader=GIT_AUTOMATION_AUTHORIZATION"));
        Assert.IsTrue(processRunner.Arguments[2].Contains(
            "--config-env=http.https://dev.azure.com/.extraHeader=GIT_AUTOMATION_AUTHORIZATION"));
        Assert.IsFalse(processRunner.Arguments
            .SelectMany(arguments => arguments)
            .Any(argument => argument.Contains("test-value", StringComparison.Ordinal)));
        Assert.IsTrue(processRunner.EnvironmentVariables[0]!["GIT_AUTOMATION_AUTHORIZATION"]
            .StartsWith("AUTHORIZATION: Bearer ", StringComparison.Ordinal));
        Assert.IsNull(processRunner.EnvironmentVariables[1]);
        Assert.IsTrue(processRunner.EnvironmentVariables[2]!["GIT_AUTOMATION_AUTHORIZATION"]
            .StartsWith("AUTHORIZATION: Bearer ", StringComparison.Ordinal));
        Assert.IsFalse(processRunner.Arguments
            .SelectMany(arguments => arguments)
            .Any(argument =>
                argument.StartsWith("https://", StringComparison.Ordinal)
                && argument.Contains('@', StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Fork_UsesSourceRepositoryForSearchAndCreate()
    {
        AzureDevOpsRepo fork = new(
            Repo.OrganizationUri,
            "fork project",
            "fork repo");
        QueueHttpMessageHandler handler = new(
            """{"id":"11111111-1111-1111-1111-111111111111"}""",
            """{"value":[]}""",
            """
            {"pullRequestId":42}
            """);
        AzureDevOpsRepoHost host = CreateHost(handler, sourceRepo: fork);

        ExistingPullRequest? pullRequest =
            await host.GetPullRequest("automation/update", CancellationToken.None);
        IReadOnlyList<IOperationResult> results = await host.ExecuteAsync(
            [new CreatePullRequestOperation("Title", "Body", "automation/update", "main")],
            CancellationToken.None);

        Assert.IsNull(pullRequest);
        Assert.HasCount(1, results);
        StringAssert.Contains(
            handler.Requests[0].Uri.AbsoluteUri,
            "fork%20project/_apis/git/repositories/fork%20repo");
        StringAssert.Contains(
            handler.Requests[1].Uri.Query,
            "searchCriteria.sourceRepositoryId=11111111-1111-1111-1111-111111111111");
        StringAssert.Contains(
            handler.Requests[2].Body,
            "\"forkSource\":{\"name\":\"refs/heads/automation/update\"," +
            "\"repository\":{\"id\":\"11111111-1111-1111-1111-111111111111\"}}");
    }

    private static AzureDevOpsRepoHost CreateHost(
        QueueHttpMessageHandler handler,
        IProcessRunner? processRunner = null,
        AzureDevOpsRepo? sourceRepo = null)
    {
        HttpClient client = new(handler);
        AzureDevOpsAccess access = new(
            new AuthenticationHeaderValue("Bearer", "test-value"),
            new AutomationIdentity("Bot", "bot@example.com"),
            client);
        Git git = new(processRunner ?? new StubProcessRunner(), NullLogger.Instance);
        return new(Repo, sourceRepo ?? Repo, access, NullLoggerFactory.Instance, git);
    }

    private sealed class QueueHttpMessageHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                body));

            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responses.Dequeue(),
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? Authorization,
        string Body);

    private sealed class StubProcessRunner : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, string>? environmentVariables = null) =>
            Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<string[]> Arguments { get; } = [];
        public List<IReadOnlyDictionary<string, string>?> EnvironmentVariables { get; } = [];

        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, string>? environmentVariables = null)
        {
            string[] argumentArray = arguments.ToArray();
            Arguments.Add(argumentArray);
            EnvironmentVariables.Add(environmentVariables);
            string standardOutput = argumentArray.Contains("rev-parse")
                ? "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                : string.Empty;
            return Task.FromResult(new ProcessResult(0, standardOutput, string.Empty));
        }
    }
}
