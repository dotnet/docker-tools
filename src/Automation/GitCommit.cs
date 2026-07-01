// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// A git commit: its SHA, author, and message.
/// </summary>
/// <param name="Sha">The full commit SHA.</param>
/// <param name="AuthorName">The name of the commit's author.</param>
/// <param name="AuthorEmail">The email address of the commit's author.</param>
/// <param name="Message">
/// The commit message. For commits read back from history (e.g. during
/// foreign-commit detection) this is only the subject line; for commits the
/// automation creates it is the full message that was passed to
/// <see cref="IGitContext.CommitAsync"/>.
/// </param>
public sealed record GitCommit(string Sha, string AuthorName, string AuthorEmail, string Message);

public static class GitCommitExtensions
{
    /// <summary>
    /// The subject of the commit: the first line of its
    /// <see cref="GitCommit.Message"/>.
    /// </summary>
    public static string Subject(this GitCommit commit) =>
        commit.Message.Split('\n', 2)[0].TrimEnd('\r');
}
