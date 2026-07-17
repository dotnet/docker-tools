// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.DotNet.DockerTools.Infrastructure;

/// <summary>
/// Provides access to the <c>eng/docker-tools</c> infrastructure files (pipeline templates,
/// PowerShell scripts, and docs) that are embedded into this assembly at build time.
/// </summary>
/// <remarks>
/// A matching version of ImageBuilder ships these files so it can write them back out to disk,
/// keeping pipeline content coupled to the ImageBuilder version that consumes it.
/// </remarks>
public static class InfrastructureContent
{
    /// <summary>
    /// Prefix applied to the <c>LogicalName</c> of every embedded content resource.
    /// </summary>
    private const string ResourcePrefix = "Content/";
    private const string FileModesResourceName = "ContentFileModes.txt";

    private static readonly Assembly s_assembly = typeof(InfrastructureContent).Assembly;

    /// <summary>
    /// Maps each content file's path (relative to the embedded content root, using '/' separators)
    /// to its underlying manifest resource name.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> s_resourceNamesByPath = BuildIndex();

    private static readonly IReadOnlyDictionary<string, UnixFileMode> s_fileModesByPath = BuildFileModes();

    /// <summary>
    /// Gets the paths of all embedded content files, relative to the content root and using
    /// '/' as the directory separator (for example, <c>templates/jobs/build-images.yml</c>).
    /// </summary>
    public static IReadOnlyList<string> GetRelativePaths() => [.. s_resourceNamesByPath.Keys];

    /// <summary>
    /// Gets the Unix file mode captured for an embedded content file.
    /// </summary>
    public static UnixFileMode GetUnixFileMode(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return s_fileModesByPath.TryGetValue(relativePath, out UnixFileMode mode)
            ? mode
            : throw new KeyNotFoundException($"No embedded infrastructure content found for path '{relativePath}'.");
    }

    /// <summary>
    /// Opens a stream over the raw bytes of an embedded content file. The caller owns the returned
    /// stream and is responsible for disposing it. Returning a stream avoids buffering whole files
    /// in memory, since the embedded content can be arbitrarily large.
    /// </summary>
    /// <param name="relativePath">
    /// The file's path relative to the content root, using '/' separators, exactly as returned by
    /// <see cref="GetRelativePaths"/>.
    /// </param>
    public static Stream OpenRead(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (!s_resourceNamesByPath.TryGetValue(relativePath, out string? resourceName))
        {
            throw new KeyNotFoundException($"No embedded infrastructure content found for path '{relativePath}'.");
        }

        return s_assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be opened.");
    }

    private static Dictionary<string, string> BuildIndex()
    {
        Dictionary<string, string> resourceNamesByPath = new(StringComparer.Ordinal);

        // GetManifestResourceNames/GetManifestResourceStream are part of the AOT/trim-safe reflection subset
        // (embedded resources are preserved by the trimmer), so this is intentionally AOT compatible. Don't
        // "fix" it by switching to a source generator without a concrete reason - it would only add an analyzer
        // project and a Roslyn dependency for no AOT benefit.
        foreach (string resourceName in s_assembly.GetManifestResourceNames())
        {
            // Resource names use the build OS directory separator, so normalize before matching.
            string normalizedName = resourceName.Replace('\\', '/');
            if (!normalizedName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string relativePath = normalizedName[ResourcePrefix.Length..];
            resourceNamesByPath[relativePath] = resourceName;
        }

        return resourceNamesByPath;
    }

    private static Dictionary<string, UnixFileMode> BuildFileModes()
    {
        using Stream stream = s_assembly.GetManifestResourceStream(FileModesResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{FileModesResourceName}' could not be opened.");
        using StreamReader reader = new(stream);
        Dictionary<string, UnixFileMode> fileModesByPath = new(StringComparer.Ordinal);

        while (reader.ReadLine() is { } line)
        {
            int separatorIndex = line.IndexOf(' ');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                throw new InvalidOperationException($"Invalid infrastructure file mode entry '{line}'.");
            }

            string mode = line[..separatorIndex];
            string relativePath = line[(separatorIndex + 1)..];
            fileModesByPath.Add(relativePath, (UnixFileMode)Convert.ToInt32(mode, 8));
        }

        if (fileModesByPath.Count != s_resourceNamesByPath.Count
            || !fileModesByPath.Keys.All(s_resourceNamesByPath.ContainsKey))
        {
            throw new InvalidOperationException(
                "Embedded infrastructure content and file mode metadata do not contain the same paths.");
        }

        return fileModesByPath;
    }
}
