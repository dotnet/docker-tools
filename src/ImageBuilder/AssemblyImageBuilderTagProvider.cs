// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Reflection;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Resolves the ImageBuilder tag from the <see cref="AssemblyMetadataAttribute"/> baked into this
/// assembly at build time (via the <c>ImageBuilderTag</c> MSBuild property).
/// </summary>
internal sealed class AssemblyImageBuilderTagProvider : IImageBuilderTagProvider
{
    private const string ImageBuilderTagMetadataKey = "ImageBuilderTag";

    /// <inheritdoc/>
    public string? GetTag() =>
        typeof(AssemblyImageBuilderTagProvider).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == ImageBuilderTagMetadataKey)?.Value;
}
