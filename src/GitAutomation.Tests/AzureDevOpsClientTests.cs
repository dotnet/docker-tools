// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.DotNet.GitAutomation.AzureDevOps;

namespace Microsoft.DotNet.GitAutomation.Tests;

[TestClass]
public sealed class AzureDevOpsClientTests
{
    [TestMethod]
    public void Constructor_WithConnectionDetails_CreatesDisposableClient()
    {
        using AzureDevOpsClient client = new AzureDevOpsClient(
            "organization",
            "project",
            "access-token");

        Assert.IsNotNull(client);
    }

    [TestMethod]
    public void Dispose_WithProvidedHttpClient_DoesNotDisposeHttpClient()
    {
        TrackingHandler handler = new TrackingHandler();
        using HttpClient httpClient = new HttpClient(handler);
        AzureDevOpsClient client = new AzureDevOpsClient(httpClient);

        client.Dispose();

        Assert.IsFalse(handler.IsDisposed);

        httpClient.Dispose();

        Assert.IsTrue(handler.IsDisposed);
    }

    [TestMethod]
    public async Task ListActivePullRequestsAsync_MapsRestrictedSearchResult()
    {
        const string responseJson = """
            {
              "count": 1,
              "value": [
                {
                  "pullRequestId": 42,
                  "title": "Partial result",
                  "repository": {
                    "id": "repository-id",
                    "name": "repository"
                  }
                }
              ]
            }
            """;

        using HttpClient httpClient = new HttpClient(new StubHandler(responseJson))
        {
            BaseAddress = new Uri("https://dev.azure.com/example/project/_apis/git/repositories/"),
        };
        using AzureDevOpsClient client = new AzureDevOpsClient(httpClient);

        AzureDevOpsPullRequestSearchResult[] results = await client.ListActivePullRequestsAsync(
            "repository",
            "refs/heads/automation/update",
            CancellationToken.None);

        Assert.AreEqual(1, results.Length);
        Assert.AreEqual(42, results[0].PullRequestId);
    }

    private sealed class TrackingHandler : HttpMessageHandler
    {
        public bool IsDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = disposing;
            base.Dispose(disposing);
        }
    }

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new HttpResponseMessage
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };

            return Task.FromResult(response);
        }
    }
}
