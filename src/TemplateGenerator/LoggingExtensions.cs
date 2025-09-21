// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.DockerTools.Templating.Cottle;
using Microsoft.DotNet.DockerTools.Templating;

namespace Microsoft.DotNet.DockerTools.TemplateGenerator;

internal static class LoggingExtensions
{
    extension(FileSystem fs)
    {
        public void LogStatistics() => Console.WriteLine(
            $"""
            Read {fs.FilesRead} files ({fs.BytesRead} bytes)
            Wrote {fs.FilesWritten} files ({fs.BytesWritten} bytes)
            """
        );
    }

    extension(FileSystemCache fsCache)
    {
        public void LogStatistics() => Console.WriteLine(
            $"File system cache hits: {fsCache.CacheHits}, misses: {fsCache.CacheMisses}"
        );
    }

    extension(CottleTemplateEngine engine)
    {
        public void LogStatistics() => Console.WriteLine(
            $"Compiled template cache hits: {engine.CompiledTemplateCacheHits},"
            + $" misses: {engine.CompiledTemplateCacheMisses}"
        );
    }
}
