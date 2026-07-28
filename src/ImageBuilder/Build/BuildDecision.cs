// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// The planned build action and supporting data for a platform.
/// </summary>
/// <param name="Action">How the platform should be handled.</param>
/// <param name="ImageToReuse">Previously published metadata to reuse, when available.</param>
/// <param name="Causes">Conditions that produced the action.</param>
public sealed record BuildDecision(
    BuildAction Action,
    PlatformData? ImageToReuse,
    IReadOnlyList<BuildCause> Causes);
