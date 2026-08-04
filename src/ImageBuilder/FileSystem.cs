// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Default filesystem implementation that delegates to <see cref="File"/> and <see cref="Directory"/>.
/// </summary>
/// <remarks>
/// Every member must be a thin, one-to-one pass-through to the system API with no added behavior;
/// any higher-level logic lives in the caller.
/// </remarks>
public sealed class FileSystem : IFileSystem
{
    /// <inheritdoc/>
    public void WriteAllText(string path, string contents) =>
        File.WriteAllText(path, contents);

    /// <inheritdoc/>
    public Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, contents, cancellationToken);

    /// <inheritdoc/>
    public Stream CreateFile(string path) =>
        File.Create(path);

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) =>
        File.ReadAllBytes(path);

    /// <inheritdoc/>
    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllBytesAsync(path, cancellationToken);

    /// <inheritdoc/>
    public string ReadAllText(string path) =>
        File.ReadAllText(path);

    /// <inheritdoc/>
    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);

    /// <inheritdoc/>
    public bool FileExists(string path) =>
        File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) =>
        Directory.Exists(path);

    /// <inheritdoc/>
    public void DeleteFile(string path) =>
        File.Delete(path);

    /// <inheritdoc/>
    public void DeleteDirectory(string path, bool recursive) =>
        Directory.Delete(path, recursive);

    /// <inheritdoc/>
    public DirectoryInfo CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public string GetCurrentDirectory() =>
        Directory.GetCurrentDirectory();
}
