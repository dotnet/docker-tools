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
internal sealed class FileSystem : IFileSystem
{
    /// <inheritdoc/>
    public void WriteAllText(string path, string contents) =>
        File.WriteAllText(path, contents);

    /// <inheritdoc/>
    public Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, contents, cancellationToken);

    /// <inheritdoc/>
    public byte[] ReadAllBytes(string path) =>
        File.ReadAllBytes(path);

    /// <inheritdoc/>
    public bool FileExists(string path) =>
        File.Exists(path);

    /// <inheritdoc/>
    public void DeleteFile(string path) =>
        File.Delete(path);

    /// <inheritdoc/>
    public DirectoryInfo CreateDirectory(string path) =>
        Directory.CreateDirectory(path);
}
