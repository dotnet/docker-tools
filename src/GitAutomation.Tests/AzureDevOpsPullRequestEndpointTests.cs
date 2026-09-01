// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.DotNet.GitAutomation.AzureDevOps;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.GitAutomation.Tests;

[TestClass]
public sealed class AzureDevOpsPullRequestEndpointTests
{
    private const string RepositoryResponse = """
        {
          "id": "repository-id",
          "name": "repository",
          "remoteUrl": "https://dev.azure.com/example/project/_git/repository",
          "webUrl": "https://dev.azure.com/example/project/_git/repository"
        }
        """;

    private static readonly AutomationIdentity Identity = new("Automation", "automation@example.com");

    [TestMethod]
    public async Task GetIdentityAsync_ReturnsConfiguredIdentity()
    {
        using HttpClient httpClient = CreateHttpClient((_, _) => JsonResponse(RepositoryResponse));
        AzureDevOpsPullRequestEndpoint endpoint = CreateEndpoint(httpClient);

        AutomationIdentity identity = await endpoint.GetIdentityAsync(CancellationToken.None);

        Assert.AreSame(Identity, identity);
    }

    [TestMethod]
    public async Task CloneAndPush_UseRepositoryRemoteAndBearerToken()
    {
        int repositoryRequests = 0;
        using HttpClient httpClient = CreateHttpClient((_, _) =>
        {
            repositoryRequests++;
            return JsonResponse(RepositoryResponse);
        });

        RecordingProcessRunner processRunner = new RecordingProcessRunner();
        AzureDevOpsPullRequestEndpoint endpoint = CreateEndpoint(httpClient);

        using GitWorkingCopy workingCopy = new GitWorkingCopy(processRunner, NullLogger.Instance);

        await endpoint.CloneTargetAsync(workingCopy, "main", Identity, CancellationToken.None);
        await endpoint.PushAsync(workingCopy, "automation/update", force: true, CancellationToken.None);

        ProcessInvocation clone = processRunner.Invocations[0];
        CollectionAssert.AreEqual(
            new[]
            {
                "clone",
                "--single-branch",
                "--no-tags",
                "--branch",
                "main",
                "https://dev.azure.com/example/project/_git/repository",
                workingCopy.WorkspaceDirectory,
            },
            clone.Arguments);

        AssertAuthorization(clone);

        ProcessInvocation push = processRunner.Invocations[3];
        CollectionAssert.AreEqual(
            new[]
            {
                "push",
                "--force",
                "https://dev.azure.com/example/project/_git/repository",
                "HEAD:refs/heads/automation/update",
            },
            push.Arguments);

        AssertAuthorization(push);
        Assert.AreEqual(1, repositoryRequests);
    }

    [TestMethod]
    public async Task FindPullRequestAsync_MapsPullRequestAndCommits()
    {
        Queue<HttpResponseMessage> responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse("""
                {
                  "count": 1,
                  "value": [
                    {
                      "pullRequestId": 42,
                      "title": "Update dependencies",
                      "description": "Generated update",
                      "sourceRefName": "refs/heads/automation/update",
                      "targetRefName": "refs/heads/release/10.0",
                      "repository": {
                        "id": "repository-id",
                        "name": "repository",
                        "remoteUrl": "https://dev.azure.com/example/project/_git/repository",
                        "webUrl": "https://dev.azure.com/example/project/_git/repository"
                      },
                      "lastMergeSourceCommit": { "commitId": "source-commit" }
                    }
                  ]
                }
                """),
            JsonResponse("""
                {
                  "commitId": "source-commit",
                  "treeId": "source-tree",
                  "author": {
                    "name": "Automation",
                    "email": "automation@example.com"
                  }
                }
                """),
            JsonResponse("""
                {
                  "count": 2,
                  "value": [
                    {
                      "commitId": "first",
                      "treeId": "first-tree",
                      "author": {
                        "name": "Automation",
                        "email": "automation@example.com"
                      }
                    },
                    {
                      "commitId": "second",
                      "treeId": "second-tree",
                      "author": {
                        "name": "Contributor",
                        "email": "contributor@example.com"
                      }
                    }
                  ]
                }
                """),
        ]);

        List<Uri> requests = [];
        using HttpClient httpClient = CreateHttpClient((request, _) =>
        {
            requests.Add(request.RequestUri ?? throw new InvalidOperationException("Request URI was null."));
            return responses.Dequeue();
        });

        AzureDevOpsPullRequestEndpoint endpoint = CreateEndpoint(httpClient);

        ExistingPullRequest? pullRequest = await endpoint.FindPullRequestAsync(
            "automation/update",
            CancellationToken.None);

        Assert.IsNotNull(pullRequest);
        Assert.AreEqual(42, pullRequest.Number);
        Assert.AreEqual(
            new Uri("https://dev.azure.com/example/project/_git/repository/pullrequest/42"),
            pullRequest.Url);
        Assert.AreEqual("automation/update", pullRequest.Content.Key);
        Assert.AreEqual("Update dependencies", pullRequest.Content.Title);
        Assert.AreEqual("Generated update", pullRequest.Content.Body);
        Assert.AreEqual("release/10.0", pullRequest.Content.TargetBranch);
        Assert.AreEqual("source-tree", pullRequest.Content.TreeHash);
        Assert.AreEqual(2, pullRequest.Commits.Count);
        Assert.AreEqual(
            new CommitInfo("second", "Contributor", "contributor@example.com"),
            pullRequest.Commits[1]);
        Assert.AreEqual(
            "api-version=7.1&searchCriteria.status=active&searchCriteria.sourceRefName=refs%2Fheads%2Fautomation%2Fupdate",
            requests[0].Query.TrimStart('?'));
    }

    [TestMethod]
    public async Task FindPullRequestAsync_ReturnsNullWhenNoPullRequestExists()
    {
        using HttpClient httpClient = CreateHttpClient((_, _) => JsonResponse("""{"count":0,"value":[]}"""));
        AzureDevOpsPullRequestEndpoint endpoint = CreateEndpoint(httpClient);

        ExistingPullRequest? pullRequest = await endpoint.FindPullRequestAsync(
            "automation/update",
            CancellationToken.None);

        Assert.IsNull(pullRequest);
    }

    [TestMethod]
    public async Task CreatePullRequestAsync_UsesFullRefNamesAndReturnsWebUrl()
    {
        string? requestBody = null;
        Queue<HttpResponseMessage> responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse("""
                {
                  "pullRequestId": 42,
                  "title": "Update dependencies",
                  "description": "Generated update",
                  "sourceRefName": "refs/heads/automation/update",
                  "targetRefName": "refs/heads/main",
                  "repository": {
                    "id": "repository-id",
                    "name": "repository",
                    "remoteUrl": "https://dev.azure.com/example/project/_git/repository",
                    "webUrl": "https://dev.azure.com/example/project/_git/repository"
                  },
                  "lastMergeSourceCommit": { "commitId": "source-commit" }
                }
                """),
        ]);

        using HttpClient httpClient = CreateHttpClient(async (request, _) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                requestBody = await ReadRequestBodyAsync(request);
            }

            return responses.Dequeue();
        });

        AzureDevOpsPullRequestEndpoint endpoint = CreateEndpoint(httpClient);
        NewPullRequest pullRequest = new NewPullRequest(
            "Update dependencies",
            "Generated update",
            "automation/update",
            "main");

        Uri url = await endpoint.CreatePullRequestAsync(pullRequest, CancellationToken.None);

        Assert.AreEqual(
            new Uri("https://dev.azure.com/example/project/_git/repository/pullrequest/42"),
            url);

        using JsonDocument document = JsonDocument.Parse(
            requestBody ?? throw new InvalidOperationException("Request body was null."));

        JsonElement root = document.RootElement;
        Assert.AreEqual("refs/heads/automation/update", root.GetProperty("sourceRefName").GetString());
        Assert.AreEqual("refs/heads/main", root.GetProperty("targetRefName").GetString());
        Assert.AreEqual("Update dependencies", root.GetProperty("title").GetString());
        Assert.AreEqual("Generated update", root.GetProperty("description").GetString());
    }

    [TestMethod]
    public async Task UpdatePullRequestAsync_MapsChangesAndFullTargetRef()
    {
        string? requestBody = null;
        using HttpClient httpClient = CreateHttpClient(async (request, _) =>
        {
            requestBody = await ReadRequestBodyAsync(request);

            return JsonResponse("""
                {
                  "pullRequestId": 42,
                  "title": "New title",
                  "description": "New body",
                  "sourceRefName": "refs/heads/automation/update",
                  "targetRefName": "refs/heads/release/10.0",
                  "repository": {
                    "id": "repository-id",
                    "name": "repository",
                    "remoteUrl": "https://dev.azure.com/example/project/_git/repository",
                    "webUrl": "https://dev.azure.com/example/project/_git/repository"
                  },
                  "lastMergeSourceCommit": { "commitId": "source-commit" }
                }
                """);
        });

        AzureDevOpsPullRequestEndpoint endpoint = CreateEndpoint(httpClient);
        PullRequestChanges changes = new PullRequestChanges(
            "New title",
            "New body",
            "release/10.0");

        await endpoint.UpdatePullRequestAsync(42, changes, CancellationToken.None);

        using JsonDocument document = JsonDocument.Parse(
            requestBody ?? throw new InvalidOperationException("Request body was null."));

        JsonElement root = document.RootElement;
        Assert.AreEqual("New title", root.GetProperty("title").GetString());
        Assert.AreEqual("New body", root.GetProperty("description").GetString());
        Assert.AreEqual("refs/heads/release/10.0", root.GetProperty("targetRefName").GetString());
    }

    private static AzureDevOpsPullRequestEndpoint CreateEndpoint(HttpClient httpClient)
    {
        AzureDevOpsClient client = new AzureDevOpsClient(httpClient);
        return new AzureDevOpsPullRequestEndpoint(client, "repository", "access-token", Identity);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, int, Task<HttpResponseMessage>> sendAsync)
    {
        RecordingHttpMessageHandler handler = new RecordingHttpMessageHandler(sendAsync);
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://dev.azure.com/example/project/_apis/git/repositories/"),
        };
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, int, HttpResponseMessage> send)
    {
        return CreateHttpClient((request, count) => Task.FromResult(send(request, count)));
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static Task<string> ReadRequestBodyAsync(HttpRequestMessage request)
    {
        HttpContent content = request.Content
            ?? throw new InvalidOperationException("Request content was null.");

        return content.ReadAsStringAsync();
    }

    private static void AssertAuthorization(ProcessInvocation invocation)
    {
        Assert.IsNotNull(invocation.Environment);
        Assert.AreEqual(
            "AUTHORIZATION: Bearer access-token",
            invocation.Environment["GIT_CONFIG_VALUE_0"]);
    }

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, int, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        private int _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            int requestCount = _requestCount;
            _requestCount++;
            return sendAsync(request, requestCount);
        }
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public List<ProcessInvocation> Invocations { get; } = [];

        public Task<ProcessResult> RunAsync(
            string? workingDirectory,
            string fileName,
            IEnumerable<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken)
        {
            ProcessInvocation invocation = new ProcessInvocation(
                workingDirectory,
                fileName,
                arguments.ToArray(),
                environment);

            Invocations.Add(invocation);
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }
    }

    private sealed record ProcessInvocation(
        string? WorkingDirectory,
        string FileName,
        string[] Arguments,
        IReadOnlyDictionary<string, string>? Environment);
}
