// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Microsoft.DotNet.ImageBuilder.Models.ManifestV2;

/// <summary>
/// The model values that placeholders and templates can refer to alongside metadata, like {repoKey},
/// {os.name} and {arch}. Metadata can't use these names.
/// </summary>
internal sealed record BuiltInVariables(string RepoKey, OSRelease OS, Arch Arch)
{
    // Serialized like the manifest, so names and values match what the YAML says.
    public static JsonElement Serialize(string repoKey, Platform platform)
    {
        BuiltInVariables variables = new(repoKey, platform.OS, platform.Arch);
        return JsonSerializer.SerializeToElement(variables, ManifestLoader.JsonOptions);
    }
}
