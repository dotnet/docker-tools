// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Common settings for all automation operations.
/// </summary>
/// <param name="Token">
/// The token used to authenticate with the remote service, for both git and
/// REST API operations. May be empty for repositories that allow anonymous
/// read access when no write operations will be performed (e.g. dry runs).
/// </param>
/// <param name="Author">The author/committer identity used for commits.</param>
/// <param name="IsDryRun">
/// When true, all local operations (cloning, applying changes, diffing) are
/// performed and logged, but nothing is pushed and no pull requests are
/// created or updated.
/// </param>
public sealed record GitAutomationOptions(
    string Token,
    GitAuthor Author,
    bool IsDryRun = false);
