// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Build;

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
            context.Logger.LogInformation(
                "Dockerfile '{DockerfilePath}' has no base image, so it is considered up-to-date",
                context.Platform.DockerfilePathRelativeToManifest);
            return null;
        }

        string? currentDigestSha = await context.BaseImageResolver.ResolveDigestShaAsync(context.Platform);
        string? previousDigestSha = context.PreviousPlatform.BaseImageDigest is string previousDigest
            ?  DockerHelper.GetDigestSha(previousDigest)
            : null;

        bool baseImageDigestMatches =
            previousDigestSha?.Equals(currentDigestSha, StringComparison.OrdinalIgnoreCase) == true;

        if (baseImageDigestMatches)
        {
            context.Logger.LogInformation(
                "Base image of '{DockerfilePath}' is unchanged at digest {BaseImageDigestSha}",
                context.Platform.DockerfilePathRelativeToManifest,
                currentDigestSha);
            return null;
        }

        context.Logger.LogInformation(
            "Base image of '{DockerfilePath}' changed from digest {PreviousBaseImageDigestSha} to " +
            "{CurrentBaseImageDigestSha}",
            context.Platform.DockerfilePathRelativeToManifest,
            previousDigestSha,
            currentDigestSha);

        return BuildPlanCheckDisposition.Build;
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

        if (commitShaMatches)
        {
            context.Logger.LogInformation(
                "Dockerfile '{DockerfilePath}' is unchanged since commit {CommitUrl}",
                context.Platform.DockerfilePathRelativeToManifest,
                currentCommitUrl);
        }
        else
        {
            context.Logger.LogInformation(
                "Dockerfile '{DockerfilePath}' changed from commit {PreviousCommitUrl} to {CurrentCommitUrl}",
                context.Platform.DockerfilePathRelativeToManifest,
                context.PreviousPlatform.CommitUrl,
                currentCommitUrl);
        }

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
