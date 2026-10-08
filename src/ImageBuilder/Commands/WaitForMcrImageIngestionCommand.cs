// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMcrImageIngestionCommand : ManifestCommand<WaitForMcrImageIngestionOptions>
    {
        private readonly ILogger<WaitForMcrImageIngestionCommand> _logger;
        private readonly IMarImageIngestionReporter _imageIngestionReporter;
        private readonly IArtifactService _artifactService;

        public WaitForMcrImageIngestionCommand(
            IManifestJsonService manifestJsonService,
            ILogger<WaitForMcrImageIngestionCommand> logger,
            IMarImageIngestionReporter imageIngestionReporter,
            IArtifactService artifactService) : base(manifestJsonService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _imageIngestionReporter = imageIngestionReporter ?? throw new ArgumentNullException(nameof(imageIngestionReporter));
            _artifactService = artifactService ?? throw new ArgumentNullException(nameof(artifactService));
        }

        protected override string Description => "Waits for images to complete ingestion into MCR";

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("WAITING FOR IMAGE INGESTION");
            string imageInfoPath = _artifactService.ResolvePath(Options.ImageInfoPath);

            if (!File.Exists(imageInfoPath))
            {
                _logger.LogInformation(PipelineHelper.FormatWarningCommand(
                    "Image info file not found. Skipping image ingestion wait."));
                return;
            }

            if (!Options.IsDryRun)
            {
                ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(imageInfoPath, Manifest);
                IEnumerable<DigestInfo> imageInfos = GetImageDigestInfos(imageArtifactDetails);
                await _imageIngestionReporter.ReportImageStatusesAsync(
                    Options.MarServiceConnection,
                    imageInfos,
                    Options.IngestionOptions.WaitTimeout,
                    Options.IngestionOptions.RequeryDelay,
                    Options.MinimumQueueTime,
                    cancellationToken);
            }
        }

        private IEnumerable<DigestInfo> GetImageDigestInfos(ImageArtifactDetails imageArtifactDetails) =>
            imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images.SelectMany(image => GetImageDigestInfos(image, repo)));

        private IEnumerable<DigestInfo> GetImageDigestInfos(ImageData image, RepoData repo)
        {
            if (image.Manifest?.Digest != null)
            {
                string digestSha = DockerHelper.GetDigestSha(image.Manifest.Digest);
                yield return new DigestInfo(digestSha, Options.RepoPrefix + repo.Repo, image.Manifest.SharedTags);

                if (image.ManifestImage?.SyndicatedRepo is string syndicatedRepo)
                {
                    yield return new DigestInfo(
                        digestSha,
                        Options.RepoPrefix + syndicatedRepo,
                        image.Manifest.SharedTags);
                }
            }

            foreach (PlatformData platform in image.Platforms)
            {
                string sha = DockerHelper.GetDigestSha(platform.Digest);

                yield return new DigestInfo(sha, Options.RepoPrefix + repo.Repo, platform.SimpleTags);

                if (platform.ImageInfo?.SyndicatedRepo is string syndicatedRepo)
                {
                    yield return new DigestInfo(sha, Options.RepoPrefix + syndicatedRepo, platform.SimpleTags);
                }
            }
        }
    }
}
