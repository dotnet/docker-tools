// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Build;

public static class BuildCauseExtensions
{
    public static bool IsDirect(this BuildCause cause) =>
        cause.DependencyPath.Count == 1;
}
