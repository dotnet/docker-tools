// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Octokit;

namespace Microsoft.DotNet.GitAutomation.GitHub;

internal static class OctokitExtensions
{
    public static bool IsValid([NotNullWhen(true)] this AccessToken? accessToken) =>
        accessToken is not null
        && accessToken.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1);
}
