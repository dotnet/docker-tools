// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers;

/// <summary>
/// In-memory <see cref="IFileSystem"/> implementation for unit tests.
/// Stores file contents in a dictionary and tracks all operations for assertions.
/// </summary>
internal sealed class InMemoryFileSystem : IFileSystem
{
    private readonly Dictionary<string, byte[]> _files = [];
    private readonly HashSet<string> _directories = [];

    /// <summary>
    /// The path returned by <see cref="GetCurrentDirectory"/>. Defaults to the platform root.
    /// </summary>
    public string CurrentDirectory { get; set; } = Path.DirectorySeparatorChar.ToString();

    /// <summary>
    /// Paths written via <see cref="WriteAllText"/>, <see cref="WriteAllTextAsync"/>, or <see cref="CreateFile"/>.
    /// </summary>
    public List<string> FilesWritten { get; } = [];

    /// <summary>
    /// Paths read via <see cref="ReadAllBytes"/>, <see cref="ReadAllBytesAsync"/>, or <see cref="ReadAllTextAsync"/>.
    /// </summary>
    public List<string> FilesRead { get; } = [];

    /// <summary>
    /// Paths deleted via <see cref="DeleteFile"/>.
    /// </summary>
    public List<string> FilesDeleted { get; } = [];

    /// <summary>
    /// Paths created via <see cref="CreateDirectory"/>.
    /// </summary>
    public List<string> DirectoriesCreated { get; } = [];

    /// <summary>
    /// Paths deleted via <see cref="DeleteDirectory"/>.
    /// </summary>
    public List<string> DirectoriesDeleted { get; } = [];

    /// <summary>
    /// Seeds a file with text content before a test runs.
    /// </summary>
    public void AddFile(string path, string contents) =>
        SetFile(path, Encoding.UTF8.GetBytes(contents));

    /// <summary>
    /// Seeds a file with binary content before a test runs.
    /// </summary>
    public void AddFile(string path, byte[] contents) =>
        SetFile(path, contents);

    /// <summary>
    /// Seeds an empty directory before a test runs.
    /// </summary>
    public void AddDirectory(string path) =>
        _directories.Add(path);

    public void WriteAllText(string path, string contents)
    {
        SetFile(path, Encoding.UTF8.GetBytes(contents));
        FilesWritten.Add(path);
    }

    public Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
    {
        SetFile(path, Encoding.UTF8.GetBytes(contents ?? string.Empty));
        FilesWritten.Add(path);
        return Task.CompletedTask;
    }

    public Stream CreateFile(string path) => new CommitOnDisposeStream(this, path);

    public byte[] ReadAllBytes(string path)
    {
        FilesRead.Add(path);
        return _files.TryGetValue(path, out var bytes)
            ? bytes
            : throw new FileNotFoundException("File not found", path);
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReadAllBytes(path));

    public string ReadAllText(string path)
    {
        FilesRead.Add(path);
        return _files.TryGetValue(path, out var bytes)
            ? Encoding.UTF8.GetString(bytes)
            : throw new FileNotFoundException("File not found", path);
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        FilesRead.Add(path);
        return _files.TryGetValue(path, out var bytes)
            ? Task.FromResult(Encoding.UTF8.GetString(bytes))
            : throw new FileNotFoundException("File not found", path);
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) =>
        _directories.Contains(path)
        || _files.Keys.Any(filePath => IsUnder(path, filePath))
        || _directories.Any(directory => IsUnder(path, directory));

    public void DeleteFile(string path)
    {
        _files.Remove(path);
        FilesDeleted.Add(path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        if (!DirectoryExists(path))
        {
            // Mirror Directory.Delete, which throws when the target directory does not exist.
            throw new DirectoryNotFoundException($"Could not find a part of the path '{path}'.");
        }

        if (recursive)
        {
            foreach (string file in _files.Keys.Where(filePath => IsUnder(path, filePath)).ToList())
            {
                _files.Remove(file);
                FilesDeleted.Add(file);
            }

            foreach (string directory in _directories.Where(dir => dir == path || IsUnder(path, dir)).ToList())
            {
                _directories.Remove(directory);
                DirectoriesDeleted.Add(directory);
            }

            return;
        }

        _directories.Remove(path);
        DirectoriesDeleted.Add(path);
    }

    public DirectoryInfo CreateDirectory(string path)
    {
        _directories.Add(path);
        DirectoriesCreated.Add(path);
        return new DirectoryInfo(path);
    }

    public string GetCurrentDirectory() => CurrentDirectory;

    private static bool IsUnder(string directory, string candidate) =>
        candidate.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);

    private void SetFile(string path, byte[] bytes)
    {
        _files[path] = bytes;

        // Materialize ancestor directories so they persist independently of the file,
        // mirroring a real filesystem where deleting a file leaves its directory behind.
        string? directory = Path.GetDirectoryName(path);
        while (!string.IsNullOrEmpty(directory))
        {
            _directories.Add(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    /// <summary>
    /// Gets the text content of a file, for test assertions.
    /// </summary>
    public string GetFileText(string path) =>
        Encoding.UTF8.GetString(_files[path]);

    /// <summary>
    /// Gets the binary content of a file, for test assertions.
    /// </summary>
    public byte[] GetFileBytes(string path) => _files[path];

    /// <summary>
    /// Writable stream returned by <see cref="CreateFile"/> that commits its contents to the
    /// in-memory store when disposed, mirroring the <see cref="FileStream"/> returned by
    /// <see cref="File.Create(string)"/>.
    /// </summary>
    private sealed class CommitOnDisposeStream(InMemoryFileSystem fileSystem, string path) : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fileSystem.SetFile(path, ToArray());
                fileSystem.FilesWritten.Add(path);
            }

            base.Dispose(disposing);
        }
    }
}
