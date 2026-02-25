// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PullImagesCommand : ManifestCommand<PullImagesOptions, PullImagesOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILogger<PullImagesCommand> _logger;

        public PullImagesCommand(IManifestJsonService manifestJsonService, IDockerService dockerService, ILogger<PullImagesCommand> logger) : base(manifestJsonService)
        {
            _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override string Description => "Pulls the images described in the manifest";

        public override Task ExecuteAsync()
        {
            IEnumerable<(string Tag, string Platform)> platformTags;
            if (string.IsNullOrEmpty(Options.ImageInfoPath))
            {
                platformTags = Manifest.GetFilteredPlatforms()
                    .Where(platform => platform.Tags.Any())
                    .Select(platform => (platform.Tags.First().FullyQualifiedName, platform.PlatformLabel));
            }
            else
            {
                // We want to apply manifest filtering to the loading of the image info file. This allows, for example,
                // only images of a specific architecture to be pulled.
                ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(
                    Options.ImageInfoPath, Manifest, skipManifestValidation: true, useFilteredManifest: true);
                platformTags = imageArtifactDetails.Repos
                    .SelectMany(repo => repo.Images)
                    .SelectMany(image => image.Platforms)
                    // If the platform doesn't have an associated manifest instance, it means the manifest filter
                    // options had filtered out the platform. In that case, it doesn't apply and shouldn't be pulled.
                    .Where(platform => platform.PlatformInfo is not null && platform.SimpleTags.Any())
                    .Select(platform => (
                        TagInfo.GetFullyQualifiedName(platform.PlatformInfo!.FullRepoModelName, platform.SimpleTags.First()),
                        platform.PlatformInfo!.PlatformLabel));
            }

            platformTags = platformTags
                .Distinct()
                .ToList();

            _logger.LogInformation("PULLING IMAGES");
            foreach ((string tag, string platform) in platformTags)
            {
                _dockerService.PullImage(tag, platform, Options.IsDryRun);
            }

            if (Options.OutputVariableName is not null)
            {
                _logger.LogInformation(
                    PipelineHelper.FormatOutputVariable(
                        Options.OutputVariableName,
                        string.Join(',', platformTags.Select(platformTag => platformTag.Tag))));
            }

            return Task.CompletedTask;
        }
    }
}
