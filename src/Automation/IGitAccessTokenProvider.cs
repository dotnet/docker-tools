// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Provides access tokens for repository host operations.
/// </summary>
public interface IGitAccessTokenProvider
{
    /// <summary>
    /// Gets an access token that is valid for the current operation.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels token acquisition.</param>
    /// <returns>An access token.</returns>
    ValueTask<string> GetTokenAsync(CancellationToken cancellationToken);
}
