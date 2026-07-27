// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Build;

/// <summary>
/// A disposition that an individual build-plan check may require.
/// </summary>
public enum BuildPlanCheckDisposition
{
    Build,
    ReuseAndPublish
}

/// <summary>
/// What a check's answer depends on.
/// </summary>
public enum BuildPlanCheckScope
{
    /// <summary>
    /// Depends only on the Dockerfile and its base image, so the answer is shared by every platform
    /// built from the same Dockerfile and build args.
    /// </summary>
    ImageContent,

    /// <summary>
    /// Depends on an individual platform's published tags and recorded digest.
    /// </summary>
    PlatformPublication
}

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

/// <summary>
/// An immutable build-plan check configured with a reason and evaluation function.
/// </summary>
public sealed class BuildPlanCheck : IBuildPlanCheck
{
    private readonly Func<BuildPlanCheckContext, Task<BuildPlanCheckDisposition?>> _evaluate;

    private BuildPlanCheck(
        BuildPlanReason reason,
        BuildPlanCheckScope scope,
        Func<BuildPlanCheckContext, Task<BuildPlanCheckDisposition?>> evaluate)
    {
        Reason = reason;
        Scope = scope;
        _evaluate = evaluate;
    }

    private static BuildPlanCheck CacheDisabled { get; } = new(
        BuildPlanReason.CacheDisabled,
        BuildPlanCheckScope.ImageContent,
        context => BuildResult());

    public static BuildPlanCheck MissingImageInfo { get; } = new(
        BuildPlanReason.MissingImageInfo,
        BuildPlanCheckScope.ImageContent,
        context => BuildResultWhen(context.PreviousPlatform is null));

    public static BuildPlanCheck BaseImageChanged { get; } = new(
        BuildPlanReason.BaseImageChanged,
        BuildPlanCheckScope.ImageContent,
        EvaluateBaseImageAsync);

    public static BuildPlanCheck DockerfileChanged { get; } = new(
        BuildPlanReason.DockerfileChanged,
        BuildPlanCheckScope.ImageContent,
        EvaluateDockerfile);

    public static BuildPlanCheck MissingTags { get; } = new(
        BuildPlanReason.MissingTags,
        BuildPlanCheckScope.PlatformPublication,
        EvaluateMissingTags);

    /// <summary>
    /// The checks applied when reusing previously published images is allowed.
    /// </summary>
    public static IReadOnlyList<IBuildPlanCheck> Default { get; } =
    [
        MissingImageInfo,
        BaseImageChanged,
        DockerfileChanged,
        MissingTags
    ];

    /// <summary>
    /// The checks applied when reuse is disabled, which plan every platform for a build.
    /// </summary>
    public static IReadOnlyList<IBuildPlanCheck> NoCache { get; } = [CacheDisabled];

    /// <inheritdoc/>
    public BuildPlanReason Reason { get; }

    /// <inheritdoc/>
    public BuildPlanCheckScope Scope { get; }

    /// <inheritdoc/>
    public Task<BuildPlanCheckDisposition?> EvaluateAsync(BuildPlanCheckContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _evaluate(context);
    }

    private static Task<BuildPlanCheckDisposition?> BuildResult() =>
        Task.FromResult<BuildPlanCheckDisposition?>(BuildPlanCheckDisposition.Build);

    private static Task<BuildPlanCheckDisposition?> BuildResultWhen(bool condition) =>
        condition ?
            BuildResult() :
            Task.FromResult<BuildPlanCheckDisposition?>(null);

    private static async Task<BuildPlanCheckDisposition?> EvaluateBaseImageAsync(BuildPlanCheckContext context)
    {
        if (context.PreviousPlatform is null)
        {
            return null;
        }

        if (context.Platform.FinalStageFromImage is null)
        {
            context.Logger.LogInformation("Image does not have a base image. By default, it is considered up-to-date.");
            return null;
        }

        string? currentDigestSha = await context.BaseImageResolver.ResolveDigestShaAsync(context.Platform);
        string? previousDigestSha = context.PreviousPlatform.BaseImageDigest is string previousDigest
            ?  DockerHelper.GetDigestSha(previousDigest)
            : null;

        bool baseImageDigestMatches =
            previousDigestSha?.Equals(currentDigestSha, StringComparison.OrdinalIgnoreCase) == true;

        context.Logger.LogInformation("Image info's base image digest SHA: {ImageInfoDigestSha}", previousDigestSha);
        context.Logger.LogInformation("Latest base image digest SHA: {CurrentDigestSha}", currentDigestSha);
        context.Logger.LogInformation("Base image digests match: {BaseImageDigestMatches}", baseImageDigestMatches);

        return baseImageDigestMatches ?  null : BuildPlanCheckDisposition.Build;
    }

    private static Task<BuildPlanCheckDisposition?> EvaluateDockerfile(BuildPlanCheckContext context)
    {
        // Comparing Dockerfile commits requires the Dockerfile to be present on disk and a source
        // repo URL to form the commit URL recorded in image info. Contexts that plan against a
        // remote manifest have neither, so this check has no opinion there.
        if (context.PreviousPlatform is null || context.SourceRepoUrl is null)
        {
            return Task.FromResult<BuildPlanCheckDisposition?>(null);
        }

        string currentCommitUrl = context.GitService.GetDockerfileCommitUrl(context.Platform, context.SourceRepoUrl);
        bool commitShaMatches = context.PreviousPlatform.CommitUrl?.Equals(currentCommitUrl, StringComparison.OrdinalIgnoreCase) == true;

        context.Logger.LogInformation("Image info's Dockerfile commit: {CommitUrl}", context.PreviousPlatform.CommitUrl);
        context.Logger.LogInformation("Latest Dockerfile commit: {CurrentCommitUrl}", currentCommitUrl);
        context.Logger.LogInformation("Dockerfile commits match: {CommitShaMatches}", commitShaMatches);

        return BuildResultWhen(!commitShaMatches);
    }

    private static Task<BuildPlanCheckDisposition?> EvaluateMissingTags(
        BuildPlanCheckContext context)
    {
        if (context.PreviousPlatform is null)
        {
            return Task.FromResult<BuildPlanCheckDisposition?>(BuildPlanCheckDisposition.ReuseAndPublish);
        }

        bool hasAllTags = (context.PreviousPlatform.PlatformInfo?.Tags ?? [])
            .Select(tag => tag.Name)
            .AreEquivalent(context.PreviousPlatform.SimpleTags);

        return Task.FromResult<BuildPlanCheckDisposition?>(hasAllTags ? null : BuildPlanCheckDisposition.ReuseAndPublish);
    }
}
