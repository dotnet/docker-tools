// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Maps input and output artifact paths to the configured staging directory.
/// Use when accepting files as input or writing files as output.
/// </summary>
public interface IArtifactService
{
    /// <summary>
    /// Gets the full path to the specified artifact.
    /// Does not create the artifact or guarantee its presence.
    /// </summary>
    string ResolvePath(string artifactPath);

    /// <summary>
    /// Writes <paramref name="contents"/> to the output path, creating its directory.
    /// </summary>
    void WriteAllText(string artifactPath, string contents);
}
