// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

/// <summary>
/// A <see cref="TokenCredential"/> wrapper that caches access tokens and refreshes them
/// when they are close to expiration. This is necessary for credentials like
/// <see cref="Azure.Identity.AzurePipelinesCredential"/> that do not cache tokens internally.
/// </summary>
internal class CachingTokenCredential : TokenCredential
{
    private readonly TokenCredential _innerCredential;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private AccessToken? _cachedToken;

    /// <summary>
    /// The amount of time before token expiration at which a new token should be fetched.
    /// This ensures we don't use a token that's about to expire.
    /// </summary>
    private static readonly TimeSpan TokenRefreshBuffer = TimeSpan.FromMinutes(5);

    public CachingTokenCredential(TokenCredential innerCredential)
    {
        _innerCredential = innerCredential ?? throw new ArgumentNullException(nameof(innerCredential));
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        _semaphore.Wait(cancellationToken);
        try
        {
            if (IsTokenValid(_cachedToken))
            {
                return _cachedToken!.Value;
            }

            _cachedToken = _innerCredential.GetToken(requestContext, cancellationToken);
            return _cachedToken.Value;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (IsTokenValid(_cachedToken))
            {
                return _cachedToken!.Value;
            }

            _cachedToken = await _innerCredential.GetTokenAsync(requestContext, cancellationToken);
            return _cachedToken.Value;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static bool IsTokenValid(AccessToken? token)
    {
        if (token is null)
        {
            return false;
        }

        // Token is valid if it's not expired and won't expire within the refresh buffer
        return token.Value.ExpiresOn > DateTimeOffset.UtcNow.Add(TokenRefreshBuffer);
    }
}
