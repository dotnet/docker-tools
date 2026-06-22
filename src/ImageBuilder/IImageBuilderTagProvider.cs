// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Provides the container image tag of the currently running ImageBuilder build.
/// </summary>
public interface IImageBuilderTagProvider
{
    /// <summary>
    /// Gets the image tag this ImageBuilder build was stamped with, or <see langword="null"/> if the
    /// build was produced without a tag (for example, a local <c>dotnet build</c>).
    /// </summary>
    string? GetTag();
}
