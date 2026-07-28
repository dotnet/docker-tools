// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Resolves artifact-relative paths and writes artifact files beneath the configured staging directory.
/// </summary>
public interface IArtifactService
{
    /// <summary>
    /// Resolves <paramref name="artifactPath"/> beneath the configured artifact staging directory.
    /// Rooted paths are returned unchanged for backward compatibility.
    /// </summary>
    string ResolvePath(string artifactPath);

    /// <summary>
    /// Writes <paramref name="contents"/> to the output path, creating its directory.
    /// </summary>
    void WriteAllText(string artifactPath, string contents);
}
