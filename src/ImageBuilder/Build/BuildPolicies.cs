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

public sealed record BuildPolicyResult(
    BuildAction Action,
    IReadOnlyList<BuildReason> Reasons)
{
    public BuildPolicyResult(
        BuildAction action,
        BuildReason reason)
        : this(action, [reason])
    {
    }

    public static BuildPolicyResult None { get; } = new(BuildAction.NoAction, []);
}

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
/// highest <see cref="BuildAction"/> value wins.
/// </summary>
public sealed class CompositeBuildPolicy(
    BuildAction defaultAction,
    BuildReason defaultReason,
    params IEnumerable<IBuildPolicy> policies) : IBuildPolicy
{
    public async Task<BuildPolicyResult> EvaluateAsync(
        BuildPolicyContext context,
        CancellationToken cancellationToken = default)
    {

        BuildPolicyResult[] results = await Task.WhenAll(
            policies.Select(policy => policy.EvaluateAsync(context, cancellationToken)));

        BuildAction childAction = results
            .Select(result => result.Action)
            .DefaultIfEmpty(BuildAction.NoAction)
            .Max();

        BuildAction action = (BuildAction)Math.Max((int)childAction, (int)defaultAction);

        BuildReason[] reasons = results
            .SelectMany(result => result.Reasons)
            .ToArray();

        return new BuildPolicyResult(action, childAction == BuildAction.NoAction ? [..reasons, defaultReason] : reasons);
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
            ? new(
                BuildAction.BuildImage,
                new BuildReason("No published image metadata exists."))
            : BuildPolicyResult.None;
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
            return Task.FromResult(BuildPolicyResult.None);
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
            ? new(
                BuildAction.PublishExistingImage,
                new BuildReason(
                    $"Platform tags changed from [{string.Join(", ", publishedPlatformTags)}] " +
                    $"to [{string.Join(", ", expectedPlatformTags)}]; shared tags changed from " +
                    $"[{string.Join(", ", publishedSharedTags)}] to " +
                    $"[{string.Join(", ", expectedSharedTags)}]."))
            : BuildPolicyResult.None;
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
            return Task.FromResult(BuildPolicyResult.None);
        }

        string currentCommitUrl = _gitService.GetDockerfileCommitUrl(
            context.Target.Platform,
            sourceRepoUrl);
        bool matches = publishedImage.Image.CommitUrl.Equals(
            currentCommitUrl,
            StringComparison.OrdinalIgnoreCase);
        BuildPolicyResult result = matches
            ? new(
                BuildAction.NoAction,
                new BuildReason($"Dockerfile is unchanged at '{currentCommitUrl}'."))
            : new(
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
            return BuildPolicyResult.None;
        }

        string? fromImage = context.Target.Platform.FinalStageFromImage;
        if (fromImage is null)
        {
            return new(
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

        return matches
            ? new(
                BuildAction.NoAction,
                new BuildReason(
                    $"Base image '{publicImage}' is unchanged at '{currentValue}'."))
            : new(
                BuildAction.BuildImage,
                new BuildReason(
                    $"Base image '{publicImage}' changed from " +
                    $"'{Display(previousValue)}' to '{Display(currentValue)}'."));
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
