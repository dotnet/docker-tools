// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;

/// <inheritdoc />
public sealed class OutputService(IFileSystem fileSystem, IOptions<BuildConfiguration> buildConfigOptions)
    : IOutputService
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly BuildConfiguration _buildConfig = buildConfigOptions.Value;

    /// <inheritdoc />
    public void WriteAllText(string artifactPath, string contents)
    {
        string outputPath = ResolveArtifactPath(artifactPath);
        string outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException($"Output path '{outputPath}' has no directory.");
        _fileSystem.CreateDirectory(outputDirectory);
        _fileSystem.WriteAllText(outputPath, contents);
    }

    /// <summary>
    /// Resolves an artifact-relative path beneath the configured staging directory.
    /// Rooted paths are preserved for commands that have not migrated to relative outputs.
    /// </summary>
    private string ResolveArtifactPath(string artifactPath)
    {
        // Rooted paths are already fully resolved. Keep supporting them while callers migrate.
        if (Path.IsPathRooted(artifactPath))
        {
            return artifactPath;
        }

        // Relative paths require a configured root before they can be resolved.
        if (string.IsNullOrWhiteSpace(_buildConfig.ArtifactStagingDirectory))
        {
            throw new InvalidOperationException(
                $"{nameof(BuildConfiguration.ArtifactStagingDirectory)} is not set. "
                + "Configure it in appsettings.json or via environment variables.");
        }

        // Canonicalize both paths so traversal segments can be checked reliably.
        string outputRoot = Path.GetFullPath(_buildConfig.ArtifactStagingDirectory);
        string outputPath = Path.GetFullPath(artifactPath, outputRoot);
        string relativeOutputPath = Path.GetRelativePath(outputRoot, outputPath);

        // Reject relative paths that escape the configured artifact staging directory.
        if (relativeOutputPath == ".."
            || relativeOutputPath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Output artifact paths must remain within the artifact staging directory.",
                nameof(artifactPath));
        }

        return outputPath;
    }
}
