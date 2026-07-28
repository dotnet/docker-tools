// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Writes output files, resolving relative paths beneath the configured artifact staging directory.
/// </summary>
public interface IOutputService
{
    /// <summary>
    /// Writes <paramref name="contents"/> to the output path, creating its directory.
    /// </summary>
    void WriteAllText(string artifactPath, string contents);
}
