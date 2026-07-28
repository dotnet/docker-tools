// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Explains a condition that caused a platform to receive its build action.
/// </summary>
/// <param name="Reason">The condition that initiated the decision.</param>
/// <param name="Origin">The platform where the condition was observed.</param>
/// <param name="DependencyPath">
/// The dependency path from <paramref name="Origin"/> to the affected platform, including both.
/// </param>
public sealed record BuildCause(
    BuildPlanReason Reason,
    PlatformInfo Origin,
    IReadOnlyList<PlatformInfo> DependencyPath);
