// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class CheckBaseImagesCommand : ManifestCommand<CheckBaseImagesOptions>
{
    private readonly IEnvironmentService _environmentService;
    private readonly IBuildPlanner _buildPlanner;
    private readonly ImageDigestCache _imageDigestCache;
    private readonly ILogger<CheckBaseImagesCommand> _logger;

    public CheckBaseImagesCommand(
        IManifestJsonService manifestJsonService,
        IManifestServiceFactory manifestServiceFactory,
        IBuildPlanner buildPlanner,
        IEnvironmentService environmentService,
        ILogger<CheckBaseImagesCommand> logger)
        : base(manifestJsonService)
    {
        ArgumentNullException.ThrowIfNull(manifestServiceFactory);

        _environmentService =
            environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _buildPlanner = buildPlanner ?? throw new ArgumentNullException(nameof(buildPlanner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageDigestCache = new ImageDigestCache(
            new Lazy<IManifestService>(
                () => manifestServiceFactory.Create(Options.CredentialsOptions)));
    }

    protected override string Description =>
        "Checks whether images in the current repo use up-to-date base images";

    public override async Task ExecuteAsync()
    {
        Options.BaseImageOverrideOptions.Validate();

        ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(
            Options.ImageInfoPath,
            Manifest,
            skipManifestValidation: true);

        ImageNameResolverForMatrix imageNameResolver = new(
            Options.BaseImageOverrideOptions,
            Manifest,
            Options.RepoPrefix,
            Options.SourceRepoPrefix);
        BaseImageResolver baseImageResolver = BaseImageResolver.CreateForRegistryImages(
            _imageDigestCache,
            imageNameResolver,
            Options.IsDryRun);
        BuildPlan plan = await _buildPlanner.CreateBuildPlanAsync(
            Manifest,
            Manifest.GetFilteredPlatformsWithExternalBaseImage(),
            Manifest.GetAllPlatforms(),
            imageArtifactDetails,
            baseImageResolver,
            sourceRepoUrl: null,
            BuildPlanCheck.Default);

        string[] pathsToRebuild = plan
            .GetPlatformsToSchedule(
                [BuildPlanReason.MissingImageInfo, BuildPlanReason.BaseImageChanged])
            .Select(platform => platform.Model.Dockerfile)
            .Distinct()
            .OrderBy(path => path)
            .ToArray();

        if (pathsToRebuild.Length == 0)
        {
            _logger.LogInformation("All images are using up-to-date base images.");
            return;
        }

        _logger.LogError(
            "{StaleImageCount} Dockerfile(s) need to be rebuilt because their base images are out-of-date:",
            pathsToRebuild.Length);

        foreach (string path in pathsToRebuild)
        {
            _logger.LogError("{DockerfilePath}", path);
        }

        _environmentService.Exit(1);
    }
}
