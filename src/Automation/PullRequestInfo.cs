// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// A pull request as seen through <see cref="IPullRequestApi"/>.
/// </summary>
/// <param name="Id">The host's identifier for the pull request (e.g. its number).</param>
/// <param name="Url">A link to the pull request.</param>
/// <param name="Title">The pull request title.</param>
/// <param name="Body">The pull request description.</param>
public sealed record PullRequestInfo(long Id, string Url, string Title, string Body);
