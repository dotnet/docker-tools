// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// Input available to one build-plan check.
/// </summary>
/// <param name="Platform">Platform being evaluated.</param>
/// <param name="PreviousPlatform">Previously published platform metadata, when available.</param>
/// <param name="BaseImageResolver">Resolver for the platform's current base-image identity.</param>
/// <param name="GitService">Service for querying Dockerfile source state.</param>
/// <param name="Logger">Logger for check diagnostics.</param>
/// <param name="SourceRepoUrl">Source repository URL used to construct commit URLs.</param>
public sealed record BuildPlanCheckContext(
    PlatformInfo Platform,
    PlatformData? PreviousPlatform,
    BaseImageResolver BaseImageResolver,
    IGitService GitService,
    ILogger Logger,
    string? SourceRepoUrl);
