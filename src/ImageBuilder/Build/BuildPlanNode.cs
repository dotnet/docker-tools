// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// A platform in a build plan.
/// </summary>
/// <param name="Platform">The platform represented by this node.</param>
/// <param name="Decision">The build decision, or null when the platform was not selected for planning.</param>
/// <param name="Dependents">The platforms that consume this platform's image.</param>
public sealed record BuildPlanNode(
    PlatformInfo Platform,
    BuildDecision? Decision,
    IReadOnlyList<BuildPlanNode> Dependents);
