// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Abstraction over filesystem operations for testability.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Creates a new file, writes the specified string to the file, and then closes the file.
    /// </summary>
    void WriteAllText(string path, string contents);

    /// <summary>
    /// Asynchronously creates a new file, writes the specified string to the file, and then closes the file.
    /// </summary>
    Task WriteAllTextAsync(string path, string? contents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a binary file, reads the contents into a byte array, and then closes the file.
    /// </summary>
    byte[] ReadAllBytes(string path);

    /// <summary>
    /// Asynchronously opens a binary file, reads the contents into a byte array, and then closes the file.
    /// </summary>
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a text file, reads all the text in the file, and then closes the file.
    /// </summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether the specified file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Creates all directories and subdirectories in the specified path unless they already exist.
    /// </summary>
    DirectoryInfo CreateDirectory(string path);
}
