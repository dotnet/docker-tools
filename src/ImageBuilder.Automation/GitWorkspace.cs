// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder.Automation;

/// <summary>
/// A commit as seen by foreign-commit detection: who authored it and what it
/// says, but not its contents.
/// </summary>
internal sealed record GitCommit(string Sha, string AuthorName, string AuthorEmail, string Subject);

/// <summary>
/// A temporary local clone of a remote repository. Deleted on dispose.
/// </summary>
internal sealed class GitWorkspace : IDisposable
{
    private readonly GitRunner _git;
    private readonly ILogger _logger;

    private GitWorkspace(string path, GitRunner git, ILogger logger)
    {
        Path = path;
        _git = git;
        _logger = logger;
    }

    /// <summary>
    /// The root directory of the clone's working tree.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Clones a single branch of a repository into a temporary directory.
    /// The clone excludes blobs that aren't needed for the checkout
    /// (--filter=blob:none) to keep it fast on repositories with large
    /// histories, while still having the full commit history needed to push
    /// from the clone.
    /// </summary>
    public static async Task<GitWorkspace> CloneAsync(
        Uri cloneUrl, string branch, GitAuthor author, GitRunner git, ILogger logger)
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"git-workspace-{System.IO.Path.GetRandomFileName()}");

        await git.RunAsync(
            workingDirectory: null,
            "clone",
            "--filter=blob:none",
            "--single-branch",
            "--no-tags",
            "--branch", branch,
            cloneUrl.AbsoluteUri,
            path);

        await git.RunAsync(path, "config", "user.name", author.Name);
        await git.RunAsync(path, "config", "user.email", author.Email);

        return new GitWorkspace(path, git, logger);
    }

    /// <summary>
    /// Creates or resets a branch at <paramref name="startPoint"/> (the
    /// current commit when null) and checks it out.
    /// </summary>
    public Task CheckoutNewBranchAsync(string branch, string? startPoint = null) =>
        _git.RunAsync(
            Path,
            startPoint is null ? ["checkout", "-B", branch] : ["checkout", "-B", branch, startPoint]);

    /// <summary>
    /// Returns whether a branch exists on a remote repository. Throws
    /// <see cref="GitException"/> if the remote cannot be reached.
    /// </summary>
    public async Task<bool> RemoteBranchExistsAsync(Uri remoteUrl, string branch) =>
        !string.IsNullOrWhiteSpace(
            await _git.RunAsync(Path, "ls-remote", remoteUrl.AbsoluteUri, $"refs/heads/{branch}"));

    /// <summary>
    /// Fetches a branch from a remote repository into FETCH_HEAD.
    /// </summary>
    public Task FetchAsync(Uri remoteUrl, string branch) =>
        _git.RunAsync(Path, "fetch", remoteUrl.AbsoluteUri, branch);

    /// <summary>
    /// Returns whether the working tree has any changes (including untracked files).
    /// </summary>
    public async Task<bool> HasChangesAsync() =>
        !string.IsNullOrWhiteSpace(await _git.RunAsync(Path, "status", "--porcelain"));

    /// <summary>
    /// Logs a summary of the working tree changes, and the full diff at debug level.
    /// </summary>
    public async Task LogChangesAsync()
    {
        string status = await _git.RunAsync(Path, "status", "--porcelain");
        _logger.LogInformation("Working tree changes:{NewLine}{Status}", Environment.NewLine, EscapeVsoDirectives(status));

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            // Stage everything so that new files show up in the diff.
            await _git.RunAsync(Path, "add", "--all");
            string diff = await _git.RunAsync(Path, "diff", "--cached");
            _logger.LogDebug("Full diff:{NewLine}{Diff}", Environment.NewLine, EscapeVsoDirectives(diff));
        }
    }

    /// <summary>
    /// Stages all working tree changes and returns the SHA of the tree they
    /// describe, without creating a commit. Comparing this against another
    /// commit's tree SHA answers "would committing this produce identical
    /// content?".
    /// </summary>
    public async Task<string> StageAllAndGetTreeAsync()
    {
        await _git.RunAsync(Path, "add", "--all");
        return await _git.RunAsync(Path, "write-tree");
    }

    /// <summary>
    /// Stages and commits all working tree changes. Returns the new commit's SHA.
    /// </summary>
    public async Task<string> CommitAllAsync(string message)
    {
        await _git.RunAsync(Path, "add", "--all");
        await _git.RunAsync(Path, "commit", "--message", message);
        return await _git.RunAsync(Path, "rev-parse", "HEAD");
    }

    /// <summary>
    /// Resolves a git revision (e.g. "HEAD^{tree}") to an object SHA.
    /// </summary>
    public Task<string> RevParseAsync(string revision) =>
        _git.RunAsync(Path, "rev-parse", revision);

    /// <summary>
    /// Lists the commits reachable from <paramref name="tip"/> but not from
    /// <paramref name="excludeReachableFrom"/>, most recent first.
    /// </summary>
    public async Task<IReadOnlyList<GitCommit>> GetCommitsAsync(string tip, string excludeReachableFrom)
    {
        const char FieldSeparator = '\x1f';
        string output = await _git.RunAsync(
            Path, "log", "--format=%H%x1f%an%x1f%ae%x1f%s", tip, "--not", excludeReachableFrom);

        if (string.IsNullOrEmpty(output))
        {
            return [];
        }

        return
        [
            .. output.Split('\n').Select(line =>
            {
                string[] fields = line.Split(FieldSeparator);
                return new GitCommit(fields[0], fields[1], fields[2], fields[3]);
            }),
        ];
    }

    /// <summary>
    /// Pushes HEAD to the given branch on a remote repository. Without
    /// <paramref name="force"/>, the push only succeeds if it is a
    /// fast-forward.
    /// </summary>
    public Task PushAsync(Uri remoteUrl, string branch, bool force = false)
    {
        List<string> args = ["push"];
        if (force)
        {
            args.Add("--force");
        }

        args.AddRange([remoteUrl.AbsoluteUri, $"HEAD:refs/heads/{branch}"]);
        return _git.RunAsync(Path, [.. args]);
    }

    public void Dispose()
    {
        try
        {
            DeleteDirectory(new DirectoryInfo(Path));
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (Exception e)
        {
            _logger.LogWarning("Failed to delete temporary directory {Path}: {Message}", Path, e.Message);
        }
    }

    private static void DeleteDirectory(DirectoryInfo directory)
    {
        // Files under .git are read-only and must be made writable before deletion on Windows.
        foreach (FileSystemInfo info in directory.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
        {
            info.Attributes = FileAttributes.Normal;
        }

        directory.Delete(recursive: true);
    }

    /// <summary>
    /// "Escapes" Azure DevOps logging directives in repo content so that
    /// logging it in a pipeline cannot trigger pipeline commands.
    /// See https://github.com/dotnet/docker-tools/issues/1388.
    /// </summary>
    private static string EscapeVsoDirectives(string text) =>
        text.Replace("##vso", "#VSO_DIRECTIVE");
}
