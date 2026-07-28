// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// The dependencies between platforms and the build decision for each selected platform.
/// </summary>
/// <param name="DependenciesByPlatform">The platforms that each platform directly depends on.</param>
/// <param name="DecisionsByPlatform">The build decision for each selected platform.</param>
public sealed record BuildPlan(
    IReadOnlyDictionary<PlatformInfo, IReadOnlyList<PlatformInfo>> DependenciesByPlatform,
    IReadOnlyDictionary<PlatformInfo, PlannedPlatform> DecisionsByPlatform);
