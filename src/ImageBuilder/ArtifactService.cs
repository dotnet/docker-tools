// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder;

/// <inheritdoc />
public sealed class ArtifactService(IFileSystem fileSystem, IOptions<BuildConfiguration> buildConfigOptions)
    : IArtifactService
{
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly BuildConfiguration _buildConfig = buildConfigOptions.Value;

    /// <inheritdoc />
    public void WriteAllText(string artifactPath, string contents)
    {
        string outputPath = ResolvePath(artifactPath);
        string outputDirectory = Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException($"Output path '{outputPath}' has no directory.");
        _fileSystem.CreateDirectory(outputDirectory);
        _fileSystem.WriteAllText(outputPath, contents);
    }

    /// <inheritdoc />
    public string ResolvePath(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(_buildConfig.ArtifactStagingDirectory))
        {
            throw new InvalidOperationException(
                $"{nameof(BuildConfiguration.ArtifactStagingDirectory)} is not set. "
                + "Configure it in appsettings.json or via environment variables.");
        }

        // Canonicalize both paths so traversal segments can be checked reliably.
        string artifactRoot = Path.GetFullPath(_buildConfig.ArtifactStagingDirectory);
        string resolvedArtifactPath = Path.GetFullPath(artifactPath, artifactRoot);
        string relativeArtifactPath = Path.GetRelativePath(artifactRoot, resolvedArtifactPath);

        // Reject relative paths that escape the configured artifact staging directory.
        if (relativeArtifactPath == ".."
            || relativeArtifactPath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Artifact paths must remain within the artifact staging directory.",
                nameof(artifactPath));
        }

        return resolvedArtifactPath;
    }
}
