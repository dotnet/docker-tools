// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace TemplateTool;

internal static class ManifestArgument
{
    public static Argument<FileInfo> Create() => new("manifest")
    {
        Description = "The manifest. Template and Dockerfile paths are relative to its directory."
    };
}
