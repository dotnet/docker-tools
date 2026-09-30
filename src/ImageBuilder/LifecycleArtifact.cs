// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Oras;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// A lifecycle metadata referrer (<see cref="OciArtifactType.Lifecycle"/>) attached to an image.
/// See <see cref="LifecycleAnnotations"/> for the lifecycle metadata it carries.
/// </summary>
/// <param name="Referrer">The lifecycle referrer.</param>
public sealed record LifecycleArtifact(ReferrerInfo Referrer);
