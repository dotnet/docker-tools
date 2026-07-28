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
        string outputPath = GetOutputPath(artifactPath);
        _fileSystem.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        _fileSystem.WriteAllText(outputPath, contents);
    }

    private string GetOutputPath(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(_buildConfig.ArtifactStagingDirectory))
        {
            throw new InvalidOperationException(
                $"{nameof(BuildConfiguration.ArtifactStagingDirectory)} is not set. "
                + "Configure it in appsettings.json or via environment variables.");
        }

        if (Path.IsPathRooted(artifactPath))
        {
            throw new ArgumentException("Output artifact paths must be relative.", nameof(artifactPath));
        }

        string outputRoot = Path.GetFullPath(_buildConfig.ArtifactStagingDirectory);
        string outputPath = Path.GetFullPath(artifactPath, outputRoot);
        string relativeOutputPath = Path.GetRelativePath(outputRoot, outputPath);
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
