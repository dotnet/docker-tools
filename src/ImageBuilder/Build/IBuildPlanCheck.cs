// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Evaluates one independent condition that contributes to a build plan.
/// </summary>
/// <remarks>
/// A check returns <see langword="null"/> when it has no opinion, either because the condition
/// does not apply or because the inputs it needs are unavailable in the current context.
/// </remarks>
public interface IBuildPlanCheck
{
    /// <summary>Gets the reason produced when this check fails.</summary>
    BuildPlanReason Reason { get; }

    /// <summary>Gets what this check's answer depends on.</summary>
    BuildPlanCheckScope Scope { get; }

    /// <summary>
    /// Evaluates the condition and returns its required disposition when it affects the plan.
    /// </summary>
    Task<BuildPlanCheckDisposition?> EvaluateAsync(BuildPlanCheckContext context);
}
