// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.DockerTools.Templating.Abstractions;

namespace Microsoft.DotNet.DockerTools.Templating;

public sealed class FileSystemCache : IFileSystem
{
    private readonly IFileSystem _fileSystem;
    private readonly ICache<string> _cache;

    public int CacheHits => _cache.Hits;
    public int CacheMisses => _cache.Misses;

    public FileSystemCache(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
        _cache = new ForeverCache<string>(key => _fileSystem.ReadAllText(key));
    }

    public string ReadAllText(string path)
    {
        var content = _cache.GetOrAdd(path);
        return content;
    }

    public void WriteAllText(string path, string content)
    {
        _fileSystem.WriteAllText(path, content);
    }
}
