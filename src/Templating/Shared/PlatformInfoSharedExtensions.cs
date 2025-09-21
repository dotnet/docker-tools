// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ImageBuilder.ReadModel;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.DockerTools.Templating.Cottle;

namespace Microsoft.DotNet.DockerTools.Templating.Shared;

internal static class PlatformInfoSharedExtensions
{
    extension(PlatformInfo platform)
    {
        public string OSDisplayName => platform.Model.OS switch
        {
            OS.Windows => OsHelper.GetWindowsOSDisplayName(platform.BaseOsVersion),
            _ => OsHelper.GetLinuxOSDisplayName(platform.BaseOsVersion)
        };
    }
}
