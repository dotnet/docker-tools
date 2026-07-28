// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// A platform in the build plan.
/// </summary>
/// <param name="Platform">The platform represented by this node.</param>
/// <param name="Action">How the platform should be handled, or null when it was not selected for planning.</param>
/// <param name="ImageToReuse">Previously published metadata to reuse, when available.</param>
/// <param name="Causes">Conditions that produced the action.</param>
/// <param name="Dependents">Platforms that consume this platform's image.</param>
public sealed record PlannedPlatform(
    PlatformInfo Platform,
    BuildAction? Action,
    PlatformData? ImageToReuse,
    IReadOnlyList<BuildCause> Causes,
    IReadOnlyList<PlannedPlatform> Dependents);
