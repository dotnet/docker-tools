// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Moq.Protected;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class LoggingHandlerTests
{
    /// <summary>
    /// Validates that when an HTTP timeout occurs (OperationCanceledException with TimeoutException inner),
    /// LoggingHandler logs a warning containing "HTTP timeout" and re-throws.
    /// </summary>
    [Fact]
    public async Task SendAsync_Timeout_LogsWarningAndThrows()
    {
        Mock<ILogger> mockLogger = CreateMockLogger();
        HttpMessageHandler stubInner = CreateThrowingHandler(
            new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
                new TimeoutException("A task was canceled.")));

        using LoggingHandler handler = new(mockLogger.Object) { InnerHandler = stubInner };
        using HttpMessageInvoker invoker = new(handler);

        HttpRequestMessage request = new(HttpMethod.Get, "https://example.com/v2/test/manifests/latest");

        await Should.ThrowAsync<TaskCanceledException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        mockLogger.VerifyLog(LogLevel.Warning, "HTTP timeout", Times.Once());
    }

    /// <summary>
    /// Validates that when a caller-driven cancellation occurs (OperationCanceledException without
    /// TimeoutException inner), LoggingHandler logs "HTTP canceled" rather than "HTTP timeout".
    /// </summary>
    [Fact]
    public async Task SendAsync_CallerCancellation_LogsCanceledAndThrows()
    {
        Mock<ILogger> mockLogger = CreateMockLogger();
        HttpMessageHandler stubInner = CreateThrowingHandler(
            new OperationCanceledException("The operation was canceled."));

        using LoggingHandler handler = new(mockLogger.Object) { InnerHandler = stubInner };
        using HttpMessageInvoker invoker = new(handler);

        HttpRequestMessage request = new(HttpMethod.Get, "https://example.com/v2/test/manifests/latest");

        await Should.ThrowAsync<OperationCanceledException>(
            () => invoker.SendAsync(request, CancellationToken.None));

        mockLogger.VerifyLog(LogLevel.Warning, "HTTP canceled", Times.Once());
        mockLogger.VerifyLog(LogLevel.Warning, "HTTP timeout", Times.Never());
    }

    private static Mock<ILogger> CreateMockLogger()
    {
        Mock<ILogger> mock = new();
        mock.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return mock;
    }

    private static HttpMessageHandler CreateThrowingHandler(Exception exception)
    {
        Mock<HttpMessageHandler> mock = new();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);
        return mock.Object;
    }
}

internal static class MockLoggerExtensions
{
    /// <summary>
    /// Verifies that the logger was called at the specified level with a message containing the expected substring.
    /// </summary>
    public static void VerifyLog(this Mock<ILogger> mockLogger, LogLevel level, string messageSubstring, Times times)
    {
        mockLogger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(messageSubstring)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
