// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.DockerTools.Templating.Abstractions;

namespace Microsoft.DotNet.DockerTools.Templating;

public sealed class FileSystem : IFileSystem
{
    private int _reads = 0;
    private int _bytesRead = 0;
    private int _writes = 0;
    private int _bytesWritten = 0;

    public int FilesRead => _reads;
    public int BytesRead => _bytesRead;
    public int FilesWritten => _writes;
    public int BytesWritten => _bytesWritten;

    public string ReadAllText(string path)
    {
        string content = File.ReadAllText(path);

        _bytesRead += GetBytes(content);
        _reads += 1;

        return content;
    }

    public void WriteAllText(string path, string content)
    {
        File.WriteAllText(path, content);

        _bytesWritten += GetBytes(content);
        _writes += 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBytes(string content) => System.Text.Encoding.UTF8.GetByteCount(content);
}
