// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// A disposition that an individual build-plan check may require.
/// </summary>
public enum BuildPlanCheckDisposition
{
    Build,
    ReuseAndPublish
}
