// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.ImageBuilder.ReadModel;

internal static class PathHelper
{
    [return: NotNullIfNotNull(nameof(optionalFilePath))]
    public static string? MaybeCombine(string basePath, string? optionalFilePath) =>
        (basePath, optionalFilePath) switch
        {
            // If the optionalFilePath is null, then we want to maintain that null value.
            (_, null) => null,
            _ => Path.Combine(basePath, optionalFilePath)
        };
}
