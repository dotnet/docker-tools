// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Build;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Reports images whose base images are out-of-date, using the manifest in the current repo.
/// </summary>
/// <remarks>
/// This answers the same question as <see cref="GetStaleImagesCommand"/> for a single local
/// manifest, rather than for the manifests named by a set of subscriptions.
/// </remarks>
public class GetStaleImagesLocalCommand : ManifestCommand<GetStaleImagesLocalOptions>
{
    private readonly IEnvironmentService _environmentService;
    private readonly IBuildPlanner _buildPlanner;
    private readonly ImageDigestCache _imageDigestCache;
    private readonly ILogger<GetStaleImagesLocalCommand> _logger;

    public GetStaleImagesLocalCommand(
        IManifestJsonService manifestJsonService,
        IManifestServiceFactory manifestServiceFactory,
        IBuildPlanner buildPlanner,
        IEnvironmentService environmentService,
        ILogger<GetStaleImagesLocalCommand> logger)
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
        "Gets paths to images in the current repo whose base images are out-of-date";

    public override async Task ExecuteAsync()
    {
        Options.BaseImageOverrideOptions.Validate();

        ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(
            Options.ImageInfoPath,
            Manifest,
            skipManifestValidation: true);

        string[] pathsToRebuild = [..(await StaleImageHelper.GetStaleDockerfilePathsAsync(
                _buildPlanner,
                Manifest,
                imageArtifactDetails,
                _imageDigestCache,
                Options.BaseImageOverrideOptions,
                Options.SourceRepoPrefix,
                Options.IsDryRun))
            .OrderBy(path => path)];

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
