// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// The graph of platforms and their build decisions.
/// </summary>
/// <param name="Roots">Platforms that do not depend on another platform in the manifest topology.</param>
public sealed record BuildPlan(IReadOnlyList<PlannedPlatform> Roots);
