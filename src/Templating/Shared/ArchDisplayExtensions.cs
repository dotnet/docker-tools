// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.DockerTools.Templating.Shared;

internal static class ArchDisplayExtensions
{
    extension(Architecture architecture)
    {
        public string GetDisplayName(string? variant = null)
        {
            string displayName = architecture switch
            {
                Architecture.ARM => "arm32",
                _ => architecture.ToString().ToLowerInvariant(),
            };

            if (variant != null)
            {
                displayName += variant.ToLowerInvariant();
            }

            return displayName;
        }

    }
}
