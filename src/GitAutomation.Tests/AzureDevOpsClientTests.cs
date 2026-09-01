// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
}
