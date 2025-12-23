// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class CachingTokenCredentialTests
{
    private static readonly string[] TestScopes = ["https://test.scope/.default"];
    private static readonly TokenRequestContext TestRequestContext = new(TestScopes);

    [Fact]
    public void GetToken_CachesTokenOnFirstCall()
    {
        // Arrange
        var expectedToken = CreateToken(expiresInMinutes: 60);
        var innerCredential = new Mock<TokenCredential>();
        innerCredential
            .Setup(c => c.GetToken(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(expectedToken);

        var cachingCredential = new CachingTokenCredential(innerCredential.Object);

        // Act
        var token1 = cachingCredential.GetToken(TestRequestContext, CancellationToken.None);
        var token2 = cachingCredential.GetToken(TestRequestContext, CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken.Token, token1.Token);
        Assert.Equal(expectedToken.Token, token2.Token);

        // Verify the inner credential was only called once (token was cached)
        innerCredential.Verify(
            c => c.GetToken(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GetToken_RefreshesExpiredToken()
    {
        // Arrange
        var expiredToken = CreateToken(expiresInMinutes: 4); // Expires in 4 minutes (within 5-minute buffer)
        var freshToken = CreateToken(expiresInMinutes: 60);

        var innerCredential = new Mock<TokenCredential>();
        innerCredential
            .SetupSequence(c => c.GetToken(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(expiredToken)
            .Returns(freshToken);

        var cachingCredential = new CachingTokenCredential(innerCredential.Object);

        // Act - First call returns expired token
        var token1 = cachingCredential.GetToken(TestRequestContext, CancellationToken.None);

        // Second call should refresh since token is about to expire
        var token2 = cachingCredential.GetToken(TestRequestContext, CancellationToken.None);

        // Assert
        Assert.Equal(expiredToken.Token, token1.Token);
        Assert.Equal(freshToken.Token, token2.Token);

        // Verify the inner credential was called twice (once for initial, once for refresh)
        innerCredential.Verify(
            c => c.GetToken(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetTokenAsync_CachesTokenOnFirstCall()
    {
        // Arrange
        var expectedToken = CreateToken(expiresInMinutes: 60);
        var innerCredential = new Mock<TokenCredential>();
        innerCredential
            .Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedToken);

        var cachingCredential = new CachingTokenCredential(innerCredential.Object);

        // Act
        var token1 = await cachingCredential.GetTokenAsync(TestRequestContext, CancellationToken.None);
        var token2 = await cachingCredential.GetTokenAsync(TestRequestContext, CancellationToken.None);

        // Assert
        Assert.Equal(expectedToken.Token, token1.Token);
        Assert.Equal(expectedToken.Token, token2.Token);

        // Verify the inner credential was only called once (token was cached)
        innerCredential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTokenAsync_RefreshesExpiredToken()
    {
        // Arrange
        var expiredToken = CreateToken(expiresInMinutes: 4); // Expires in 4 minutes (within 5-minute buffer)
        var freshToken = CreateToken(expiresInMinutes: 60);

        var innerCredential = new Mock<TokenCredential>();
        innerCredential
            .SetupSequence(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken)
            .ReturnsAsync(freshToken);

        var cachingCredential = new CachingTokenCredential(innerCredential.Object);

        // Act - First call returns expired token
        var token1 = await cachingCredential.GetTokenAsync(TestRequestContext, CancellationToken.None);

        // Second call should refresh since token is about to expire
        var token2 = await cachingCredential.GetTokenAsync(TestRequestContext, CancellationToken.None);

        // Assert
        Assert.Equal(expiredToken.Token, token1.Token);
        Assert.Equal(freshToken.Token, token2.Token);

        // Verify the inner credential was called twice (once for initial, once for refresh)
        innerCredential.Verify(
            c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public void GetToken_ValidTokenNotRefreshed()
    {
        // Arrange
        var validToken = CreateToken(expiresInMinutes: 30); // Expires in 30 minutes (well beyond 5-minute buffer)

        var innerCredential = new Mock<TokenCredential>();
        innerCredential
            .Setup(c => c.GetToken(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(validToken);

        var cachingCredential = new CachingTokenCredential(innerCredential.Object);

        // Act - Multiple calls
        var token1 = cachingCredential.GetToken(TestRequestContext, CancellationToken.None);
        var token2 = cachingCredential.GetToken(TestRequestContext, CancellationToken.None);
        var token3 = cachingCredential.GetToken(TestRequestContext, CancellationToken.None);

        // Assert - All tokens should be the same
        Assert.Equal(validToken.Token, token1.Token);
        Assert.Equal(validToken.Token, token2.Token);
        Assert.Equal(validToken.Token, token3.Token);

        // Verify the inner credential was only called once
        innerCredential.Verify(
            c => c.GetToken(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_ThrowsOnNullCredential()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CachingTokenCredential(null!));
    }

    [Fact]
    public async Task GetTokenAsync_ConcurrentCalls_OnlyFetchesOnce()
    {
        // Arrange
        var expectedToken = CreateToken(expiresInMinutes: 60);
        var callCount = 0;

        var innerCredential = new Mock<TokenCredential>();
        innerCredential
            .Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                Interlocked.Increment(ref callCount);
                // Simulate a slow token fetch
                Thread.Sleep(100);
                return expectedToken;
            });

        var cachingCredential = new CachingTokenCredential(innerCredential.Object);

        // Act - Start multiple concurrent token requests
        var tasks = new Task<AccessToken>[10];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = cachingCredential.GetTokenAsync(TestRequestContext, CancellationToken.None).AsTask();
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All results should be the same token
        foreach (var result in results)
        {
            Assert.Equal(expectedToken.Token, result.Token);
        }

        // Verify the inner credential was only called once despite concurrent requests
        Assert.Equal(1, callCount);
    }

    private static AccessToken CreateToken(int expiresInMinutes)
    {
        return new AccessToken(
            accessToken: Guid.NewGuid().ToString(),
            expiresOn: DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes));
    }
}
