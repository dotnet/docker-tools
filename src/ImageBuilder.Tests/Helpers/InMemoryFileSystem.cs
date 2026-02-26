// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
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
    /// Paths written via <see cref="WriteAllText"/> or <see cref="WriteAllTextAsync"/>.
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
    /// Seeds a file with text content before a test runs.
    /// </summary>
    public void AddFile(string path, string contents) =>
        _files[path] = Encoding.UTF8.GetBytes(contents);

    /// <summary>
    /// Seeds a file with binary content before a test runs.
    /// </summary>
    public void AddFile(string path, byte[] contents) =>
        _files[path] = contents;

    public void WriteAllText(string path, string contents)
    {
        _files[path] = Encoding.UTF8.GetBytes(contents);
        FilesWritten.Add(path);
    }

    public Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default)
    {
        _files[path] = Encoding.UTF8.GetBytes(contents ?? string.Empty);
        FilesWritten.Add(path);
        return Task.CompletedTask;
    }

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

    public void DeleteFile(string path)
    {
        _files.Remove(path);
        FilesDeleted.Add(path);
    }

    public DirectoryInfo CreateDirectory(string path)
    {
        _directories.Add(path);
        DirectoriesCreated.Add(path);
        return new DirectoryInfo(path);
    }

    /// <summary>
    /// Gets the text content of a file, for test assertions.
    /// </summary>
    public string GetFileText(string path) =>
        Encoding.UTF8.GetString(_files[path]);
}
