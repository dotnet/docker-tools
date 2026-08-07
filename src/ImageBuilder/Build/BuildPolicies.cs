// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Build;

public sealed record BuildPolicyContext(
    BuildGraph Graph,
    BuildTarget Target,
    IReadOnlyDictionary<BuildTarget, PublishedImage> PublishedImages);

/// <summary>
/// Work selected by a build policy and the reason it was selected.
/// </summary>
public sealed record BuildPolicyResult(
    BuildAction Action,
    BuildReason Reason);

/// <summary>
/// Evaluates one aspect of the work required for a build target.
/// </summary>
public interface IBuildPolicy
{
    Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies every child policy and combines their results into one decision. The action with the
/// highest priority wins.
/// </summary>
public sealed class CompositeBuildPolicy(
    BuildPolicyResult defaultResult,
    ILogger logger,
    params IBuildPolicy[] policies) : IBuildPolicy
{
    public async Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        BuildPolicyResult result = defaultResult;
        foreach (IBuildPolicy policy in policies)
        {
            BuildPolicyResult policyResult =
                await policy.EvaluateAsync(context, cancellationToken);

            logger.LogDebug(
                "Build policy {BuildPolicy} for {BuildTarget} returned {Action}. {Reason}",
                policy.GetType().Name,
                context.Target.DisplayName,
                policyResult.Action,
                policyResult.Reason);

            if (policyResult.Action.GetPriority() > result.Action.GetPriority())
            {
                result = policyResult;
            }
        }

        return result;
    }
}

public sealed class AlwaysBuildPolicy(string reason = "Caching is disabled.") : IBuildPolicy
{
    public Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            new BuildPolicyResult(
                BuildAction.BuildImage,
                new BuildReason(reason)));
    }
}

public sealed class MissingPublishedImagePolicy : IBuildPolicy
{
    public Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BuildPolicyResult result = !context.PublishedImages.ContainsKey(context.Target)
            ? new BuildPolicyResult(
                BuildAction.BuildImage,
                new BuildReason("No published image metadata exists."))
            : new BuildPolicyResult(
                BuildAction.NoAction,
                new BuildReason("Published image metadata exists."));
        return Task.FromResult(result);
    }
}

public sealed class TagSetChangedPolicy : IBuildPolicy
{
    public Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.PublishedImages.TryGetValue(context.Target, out var publishedImage))
        {
            return Task.FromResult(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason(
                        "Published image metadata is unavailable, so tags cannot be compared.")));
        }

        string[] expectedPlatformTags = context.Target.Platform.Tags
            .Select(tag => tag.Name)
            .ToArray();
        string[] expectedSharedTags = context.Target.Image.SharedTags
            .Select(tag => tag.Name)
            .ToArray();
        IEnumerable<string> publishedPlatformTags =
            publishedImage.Source == context.Target
                ? publishedImage.Image.SimpleTags
                : [];
        IEnumerable<string> publishedSharedTags =
            publishedImage.Source == context.Target
                ? publishedImage.SharedTags
                : [];
        bool tagsChanged =
            !expectedPlatformTags.AreEquivalent(publishedPlatformTags) ||
            !expectedSharedTags.AreEquivalent(publishedSharedTags);

        BuildPolicyResult result = tagsChanged
            ? new BuildPolicyResult(
                BuildAction.PublishExistingImage,
                new BuildReason(
                    $"Platform tags changed from [{string.Join(", ", publishedPlatformTags)}] " +
                    $"to [{string.Join(", ", expectedPlatformTags)}]; shared tags changed from " +
                    $"[{string.Join(", ", publishedSharedTags)}] to " +
                    $"[{string.Join(", ", expectedSharedTags)}]."))
            : new BuildPolicyResult(
                BuildAction.NoAction,
                new BuildReason("Configured tags are unchanged."));
        return Task.FromResult(result);
    }
}

public sealed class DockerfileChangedPolicy(
    IGitService gitService,
    string sourceRepoUrl) : IBuildPolicy
{
    private readonly IGitService _gitService =
        gitService ?? throw new ArgumentNullException(nameof(gitService));

    public Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.PublishedImages.TryGetValue(context.Target, out var publishedImage))
        {
            return Task.FromResult(
                new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason(
                        "Published image metadata is unavailable, so the Dockerfile cannot be compared.")));
        }

        string currentCommitUrl = _gitService.GetDockerfileCommitUrl(
            context.Target.Platform,
            sourceRepoUrl);
        bool matches = publishedImage.Image.CommitUrl.Equals(
            currentCommitUrl,
            StringComparison.OrdinalIgnoreCase);
        BuildPolicyResult result = matches
            ? new BuildPolicyResult(
                BuildAction.NoAction,
                new BuildReason($"Dockerfile is unchanged at '{currentCommitUrl}'."))
            : new BuildPolicyResult(
                BuildAction.BuildImage,
                new BuildReason(
                    $"Dockerfile changed from '{publishedImage.Image.CommitUrl}' " +
                    $"to '{currentCommitUrl}'."));
        return Task.FromResult(result);
    }
}

public sealed class BaseImageChangedPolicy : IBuildPolicy
{
    private readonly ImageDigestCache _imageDigests;
    private readonly ImageNameResolver _imageNames;
    private readonly bool _isDryRun;
    private readonly bool _useLocalExternalImage;

    private BaseImageChangedPolicy(
        ImageDigestCache imageDigests,
        ImageNameResolver imageNames,
        bool useLocalExternalImage,
        bool isDryRun)
    {
        _imageDigests = imageDigests ?? throw new ArgumentNullException(nameof(imageDigests));
        _imageNames = imageNames ?? throw new ArgumentNullException(nameof(imageNames));
        _useLocalExternalImage = useLocalExternalImage;
        _isDryRun = isDryRun;
    }

    public static BaseImageChangedPolicy FromLocalImages(
        ImageDigestCache imageDigests,
        ImageNameResolver imageNames,
        bool isDryRun) =>
        new(imageDigests, imageNames, useLocalExternalImage: true, isDryRun);

    public static BaseImageChangedPolicy FromRegistry(
        ImageDigestCache imageDigests,
        ImageNameResolver imageNames,
        bool isDryRun) =>
        new(imageDigests, imageNames, useLocalExternalImage: false, isDryRun);

    public async Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.PublishedImages.TryGetValue(context.Target, out var publishedImage))
        {
            return new BuildPolicyResult(
                BuildAction.NoAction,
                new BuildReason(
                    "Published image metadata is unavailable, so the base image cannot be compared."));
        }

        string? fromImage = context.Target.Platform.FinalStageFromImage;
        if (fromImage is null)
        {
            return new BuildPolicyResult(
                BuildAction.NoAction,
                new BuildReason("The final stage has no base image."));
        }

        string? currentDigest = context.Target.Platform.IsInternalFromImage(fromImage)
            ? GetInternalBaseImageDigest(context, fromImage)
            : await GetExternalBaseImageDigestAsync(context.Target, fromImage);
        string publicImage = _imageNames.GetFromImagePublicTag(fromImage);
        string? previousValue = GetDigestSha(publishedImage.Image.BaseImageDigest);
        string? currentValue = GetDigestSha(currentDigest);
        bool matches = previousValue?.Equals(
            currentValue,
            StringComparison.OrdinalIgnoreCase) == true;

        BuildPolicyResult result = matches
            ? new BuildPolicyResult(
                BuildAction.NoAction,
                new BuildReason(
                    $"Base image '{publicImage}' is unchanged at '{currentValue}'."))
            : new BuildPolicyResult(
                BuildAction.BuildImage,
                new BuildReason(
                    $"Base image '{publicImage}' changed from " +
                    $"'{Display(previousValue)}' to '{Display(currentValue)}'."));
        return result;
    }

    private static string? GetInternalBaseImageDigest(
        BuildPolicyContext context,
        string fromImage)
    {
        BuildTarget? parent = context.Graph.Parents[context.Target].FirstOrDefault(candidate =>
            candidate.Platform.Tags
                .Concat(candidate.Image.SharedTags)
                .Any(tag => tag.FullyQualifiedName == fromImage));
        return parent is not null &&
            context.PublishedImages.TryGetValue(parent, out var publishedImage)
                ? publishedImage.Image.Digest
                : null;
    }

    private async Task<string?> GetExternalBaseImageDigestAsync(
        BuildTarget target,
        string fromImage)
    {
        if (_useLocalExternalImage)
        {
            string localImage = _imageNames.GetFromImageLocalTag(fromImage);
            return await _imageDigests.GetLocalImageDigestAsync(localImage, _isDryRun);
        }

        string registryImage = _imageNames.GetFinalStageImageNameForDigestQuery(target.Platform);
        try
        {
            return await _imageDigests.GetManifestDigestShaAsync(registryImage, _isDryRun);
        }
        catch (Exception ex) when (IsImageNotFoundException(ex))
        {
            return null;
        }
    }

    private static string? GetDigestSha(string? digest) =>
        string.IsNullOrWhiteSpace(digest)
            ? null
            : DockerHelper.GetDigestSha(digest);

    private static string Display(string? value) => value ?? "<missing>";

    private static bool IsImageNotFoundException(Exception ex) =>
        ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound } ||
        ex is RequestFailedException { Status: 404 };
}
