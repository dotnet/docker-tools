// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.Tests;

/// <summary>
/// A single commit read back from a <see cref="GitTestRepo"/>, used for
/// assertions about what the automation pushed.
/// </summary>
internal sealed record TestCommit(string Sha, string AuthorName, string AuthorEmail, string Subject);

/// <summary>
/// A throwaway git repository used as a remote in tests. It is backed by a real
/// bare repository on disk (the "remote" the library clones from and pushes to)
/// plus an internal working clone used to seed branches and commits. Read
/// helpers let tests assert on what the automation actually pushed.
/// </summary>
internal sealed class GitTestRepo : IDisposable
{
    private readonly string _rootPath;
    private readonly string _barePath;
    private readonly string _workPath;

    private GitTestRepo(string rootPath, string barePath, string workPath)
    {
        _rootPath = rootPath;
        _barePath = barePath;
        _workPath = workPath;
    }

    /// <summary>The clone URL of the bare remote, as a <c>file://</c> URI.</summary>
    public Uri Url => new(_barePath);

    /// <summary>
    /// Creates a bare remote with a single initial commit on
    /// <paramref name="defaultBranch"/>.
    /// </summary>
    public static async Task<GitTestRepo> InitAsync(
        GitAuthor initialAuthor,
        string defaultBranch,
        string seedFile = "README.md",
        string seedContent = "initial")
    {
        string rootPath = CreateTempDirectory();
        try
        {
            string barePath = Path.Combine(rootPath, "remote.git");
            string workPath = Path.Combine(rootPath, "work");

            await GitRunner.RunAsync(null, "init", "--bare", "--initial-branch", defaultBranch, barePath);

            // Allow real partial clones (the library clones with --filter=blob:none).
            // --git-dir is required because git refuses to treat a bare repo as the
            // current repository implicitly when safe.bareRepository=explicit.
            await GitRunner.RunAsync(null, $"--git-dir={barePath}", "config", "uploadpack.allowFilter", "true");

            await GitRunner.RunAsync(null, "init", "--initial-branch", defaultBranch, workPath);
            await WriteAndCommitAsync(workPath, seedFile, seedContent, initialAuthor, "Initial commit");
            await GitRunner.RunAsync(workPath, "push", new Uri(barePath).AbsoluteUri, defaultBranch);

            return new GitTestRepo(rootPath, barePath, workPath);
        }
        catch
        {
            // Don't leak the temp directory if setup fails before the instance
            // (and therefore its Dispose) exists.
            TryDeleteDirectory(rootPath);
            throw;
        }
    }

    /// <summary>
    /// Seeds a branch (created from <paramref name="fromBranch"/>) with a commit
    /// authored by <paramref name="author"/>, then pushes it to the bare remote.
    /// Use a non-automation author to simulate a foreign commit.
    /// </summary>
    public async Task<string> SeedBranchAsync(
        string branch, string fromBranch, string relativePath, string content, GitAuthor author, string message)
    {
        await GitRunner.RunAsync(_workPath, "checkout", "-B", branch, fromBranch);
        string sha = await WriteAndCommitAsync(_workPath, relativePath, content, author, message);
        await GitRunner.RunAsync(_workPath, "push", "--force", Url.AbsoluteUri, branch);
        return sha;
    }

    /// <summary>Resolves the SHA at the tip of a branch on the bare remote.</summary>
    public async Task<string> GetBranchTipAsync(string branch) =>
        (await RunInBareAsync("rev-parse", $"refs/heads/{branch}")).Trim();

    /// <summary>Returns whether a branch exists on the bare remote.</summary>
    public async Task<bool> BranchExistsAsync(string branch)
    {
        string output = (await RunInBareAsync(
            "for-each-ref", "--format=%(refname:short)", $"refs/heads/{branch}")).Trim();
        return !string.IsNullOrEmpty(output);
    }

    /// <summary>Reads the content of a file at a given revision on the bare remote.</summary>
    public Task<string> GetFileAtRefAsync(string revision, string relativePath) =>
        RunInBareAsync("show", $"{revision}:{relativePath}");

    /// <summary>Lists the commits on a branch (most recent first) on the bare remote.</summary>
    public async Task<IReadOnlyList<TestCommit>> GetCommitsAsync(string branch)
    {
        const char fieldSeparator = '\x1f';
        string output = (await RunInBareAsync(
            "log", "--format=%H%x1f%an%x1f%ae%x1f%s", $"refs/heads/{branch}")).Trim();

        if (string.IsNullOrEmpty(output))
        {
            return [];
        }

        return
        [
            .. output.Split('\n').Select(line =>
            {
                string[] fields = line.Split(fieldSeparator);
                return new TestCommit(fields[0], fields[1], fields[2], fields[3]);
            }),
        ];
    }

    public void Dispose() => TryDeleteDirectory(_rootPath);

    private Task<string> RunInBareAsync(params string[] args) =>
        GitRunner.RunAsync(null, [$"--git-dir={_barePath}", .. args]);

    private static async Task<string> WriteAndCommitAsync(
        string workPath, string relativePath, string content, GitAuthor author, string message)
    {
        string fullPath = Path.Combine(workPath, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content);
        await GitRunner.RunAsync(workPath, "add", "--all");
        await GitRunner.RunAsync(
            workPath,
            "-c", $"user.name={author.Name}",
            "-c", $"user.email={author.Email}",
            "commit", "--message", message);

        return (await GitRunner.RunAsync(workPath, "rev-parse", "HEAD")).Trim();
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"automation-test-{Path.GetRandomFileName()}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            DeleteDirectory(new DirectoryInfo(path));
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone.
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
}
