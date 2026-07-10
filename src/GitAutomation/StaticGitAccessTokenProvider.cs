// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation;

/// <summary>
/// Provides a fixed access token, such as a GitHub or Azure DevOps personal access token.
/// </summary>
public sealed class StaticGitAccessTokenProvider : IGitAccessTokenProvider
{
    private readonly string _token;

    /// <summary>
    /// Creates a provider for a fixed access token.
    /// </summary>
    /// <param name="token">The access token value.</param>
    public StaticGitAccessTokenProvider(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _token = token;
    }

    /// <inheritdoc/>
    public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_token);
    }
}
