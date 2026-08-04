// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
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

    private static readonly Assembly s_assembly = typeof(InfrastructureContent).Assembly;

    /// <summary>
    /// Maps each content file's path (relative to the embedded content root, using '/' separators)
    /// to its underlying manifest resource name.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> s_resourceNamesByPath = BuildIndex();

    /// <summary>
    /// Gets the paths of all embedded content files, relative to the content root and using
    /// '/' as the directory separator (for example, <c>templates/jobs/build-images.yml</c>).
    /// </summary>
    public static IReadOnlyList<string> GetRelativePaths() => [.. s_resourceNamesByPath.Keys];

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
}
