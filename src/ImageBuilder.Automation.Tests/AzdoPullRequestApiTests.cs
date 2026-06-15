// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Text;
using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// Unit tests for the hand-rolled Azure DevOps REST adapter: request shapes
/// (URLs, methods, auth, payloads) and response parsing. The reconciliation
/// logic on top of it is covered by the equivalence tests.
/// </summary>
[TestClass]
public class AzdoPullRequestApiTests
{
    private static readonly AzdoRepo s_repo = new("dnceng", "internal proj", "dotnet docker");

    private const string PullRequestsUrl =
        "https://dev.azure.com/dnceng/internal%20proj/_apis/git/repositories/dotnet%20docker/pullrequests";

    [TestMethod]
    public async Task FindOpen_BuildsSearchUrl_AndParsesResult()
    {
        var recorder = new RequestRecorder("""
            {
                "value": [
                    { "pullRequestId": 42, "title": "Update files", "description": "Automated update." }
                ]
            }
            """);
        AzdoPullRequestApi api = CreateApi(recorder);

        PullRequestInfo? info = await api.FindOpenAsync("auto/update-a", "main", CancellationToken.None);

        recorder.Method.ShouldBe(HttpMethod.Get);
        recorder.Url.ShouldBe(
            $"{PullRequestsUrl}?searchCriteria.status=active"
            + "&searchCriteria.sourceRefName=refs%2Fheads%2Fauto%2Fupdate-a"
            + "&searchCriteria.targetRefName=refs%2Fheads%2Fmain"
            + "&api-version=7.1");
        recorder.AuthorizationHeader.ShouldBe(
            $"Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes(":secret-token"))}");

        info.ShouldNotBeNull();
        info.Id.ShouldBe(42);
        info.Title.ShouldBe("Update files");
        info.Body.ShouldBe("Automated update.");
        info.Url.ShouldBe("https://dev.azure.com/dnceng/internal%20proj/_git/dotnet%20docker/pullrequest/42");
    }

    [TestMethod]
    public async Task FindOpen_ReturnsNull_WhenNoMatch()
    {
        AzdoPullRequestApi api = CreateApi(new RequestRecorder("""{ "value": [] }"""));

        (await api.FindOpenAsync("auto/update-a", "main", CancellationToken.None)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Create_PostsRefsAndMetadata()
    {
        var recorder = new RequestRecorder(
            """{ "pullRequestId": 7, "title": "Update files", "description": null }""");
        AzdoPullRequestApi api = CreateApi(recorder);

        PullRequestInfo info =
            await api.CreateAsync("Update files", "Body text", "auto/update-a", "main", CancellationToken.None);

        recorder.Method.ShouldBe(HttpMethod.Post);
        recorder.Url.ShouldBe($"{PullRequestsUrl}?api-version=7.1");
        JsonElement payload = recorder.JsonBody();
        payload.GetProperty("sourceRefName").GetString().ShouldBe("refs/heads/auto/update-a");
        payload.GetProperty("targetRefName").GetString().ShouldBe("refs/heads/main");
        payload.GetProperty("title").GetString().ShouldBe("Update files");
        payload.GetProperty("description").GetString().ShouldBe("Body text");

        info.Id.ShouldBe(7);
        info.Body.ShouldBe(string.Empty, "a null description should be normalized to empty");
    }

    [TestMethod]
    public async Task Update_PatchesTitleAndDescription()
    {
        var recorder = new RequestRecorder("""{ "pullRequestId": 7, "title": "New title", "description": "New body" }""");
        AzdoPullRequestApi api = CreateApi(recorder);

        await api.UpdateAsync(7, "New title", "New body", CancellationToken.None);

        recorder.Method.ShouldBe(HttpMethod.Patch);
        recorder.Url.ShouldBe($"{PullRequestsUrl}/7?api-version=7.1");
        JsonElement payload = recorder.JsonBody();
        payload.GetProperty("title").GetString().ShouldBe("New title");
        payload.GetProperty("description").GetString().ShouldBe("New body");
    }

    [TestMethod]
    public async Task GetComments_FlattensThreads_AndExcludesSystemEntries()
    {
        var recorder = new RequestRecorder("""
            {
                "value": [
                    {
                        "comments": [
                            { "content": "First comment", "commentType": "text" },
                            { "content": "Jane pushed an update.", "commentType": "system" }
                        ]
                    },
                    { "comments": [ { "content": null, "commentType": "text" } ] },
                    { "comments": null },
                    { "comments": [ { "content": "Second comment", "commentType": "text" } ] }
                ]
            }
            """);
        AzdoPullRequestApi api = CreateApi(recorder);

        IReadOnlyList<string> comments = await api.GetCommentsAsync(7, CancellationToken.None);

        recorder.Method.ShouldBe(HttpMethod.Get);
        recorder.Url.ShouldBe($"{PullRequestsUrl}/7/threads?api-version=7.1");
        comments.ShouldBe(["First comment", "Second comment"]);
    }

    [TestMethod]
    public async Task AddComment_PostsNewActiveThread()
    {
        var recorder = new RequestRecorder("""{ "comments": [] }""");
        AzdoPullRequestApi api = CreateApi(recorder);

        await api.AddCommentAsync(7, "Stop explanation", CancellationToken.None);

        recorder.Method.ShouldBe(HttpMethod.Post);
        recorder.Url.ShouldBe($"{PullRequestsUrl}/7/threads?api-version=7.1");
        JsonElement payload = recorder.JsonBody();
        payload.GetProperty("status").GetString().ShouldBe("active");
        JsonElement comment = payload.GetProperty("comments")[0];
        comment.GetProperty("content").GetString().ShouldBe("Stop explanation");
        comment.GetProperty("commentType").GetString().ShouldBe("text");
        comment.GetProperty("parentCommentId").GetInt32().ShouldBe(0);
    }

    [TestMethod]
    public async Task FailedRequest_ThrowsWithStatusCode()
    {
        var recorder = new RequestRecorder("""{ "message": "PR already exists" }""", HttpStatusCode.Conflict);
        AzdoPullRequestApi api = CreateApi(recorder);

        HttpRequestException exception = await Should.ThrowAsync<HttpRequestException>(() =>
            api.CreateAsync("t", "b", "head", "main", CancellationToken.None));

        exception.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        exception.Message.ShouldContain("PR already exists");
    }

    private static AzdoPullRequestApi CreateApi(RequestRecorder recorder) =>
        new(new HttpClient(recorder), s_repo, "secret-token");

    /// <summary>
    /// Records the single request it receives and replies with a canned JSON
    /// response.
    /// </summary>
    private sealed class RequestRecorder(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public string? Url { get; private set; }

        public string? AuthorizationHeader { get; private set; }

        public string? Body { get; private set; }

        public JsonElement JsonBody() => JsonDocument.Parse(Body.ShouldNotBeNull()).RootElement;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Url = request.RequestUri?.AbsoluteUri;
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}
