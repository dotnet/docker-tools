// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.DockerTools.ImageBuilder.Models.Image;
using Microsoft.DotNet.DockerTools.ImageBuilder.ViewModel;


#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class WaitForMcrImageIngestionCommand : ManifestCommand<WaitForMcrImageIngestionOptions, WaitForMcrImageIngestionOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;
        private readonly IMarImageIngestionReporter _imageIngestionReporter;

        [ImportingConstructor]
        public WaitForMcrImageIngestionCommand(
            ILoggerService loggerService, IMarImageIngestionReporter imageIngestionReporter)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _imageIngestionReporter = imageIngestionReporter ?? throw new ArgumentNullException(nameof(imageIngestionReporter));
        }

        protected override string Description => "Waits for images to complete ingestion into MCR";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("WAITING FOR IMAGE INGESTION");

            if (!File.Exists(Options.ImageInfoPath))
            {
                _loggerService.WriteMessage(PipelineHelper.FormatWarningCommand(
                    "Image info file not found. Skipping image ingestion wait."));
                return;
            }

            if (!Options.IsDryRun)
            {
                ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);
                IEnumerable<DigestInfo> imageInfos = GetImageDigestInfos(imageArtifactDetails);
                await _imageIngestionReporter.ReportImageStatusesAsync(imageInfos, Options.IngestionOptions.WaitTimeout, Options.IngestionOptions.RequeryDelay, Options.MinimumQueueTime);
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

                // Find all syndicated shared tags grouped by their syndicated repo
                IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = image.ManifestImage.SharedTags
                    .Where(tag => image.Manifest.SharedTags.Contains(tag.Name) && tag.SyndicatedRepo != null)
                    .GroupBy(tag => tag.SyndicatedRepo);

                foreach (IGrouping<string, TagInfo> syndicatedTags in syndicatedTagGroups)
                {
                    string syndicatedRepo = syndicatedTags.Key;
                    string fullyQualifiedRepo = DockerHelper.GetImageName(Manifest.Model.Registry, syndicatedRepo);

                    string? syndicatedDigest = image.Manifest.SyndicatedDigests
                        .FirstOrDefault(digest => digest.StartsWith($"{fullyQualifiedRepo}@"));

                    if (syndicatedDigest is null)
                    {
                        throw new InvalidOperationException($"Unable to find syndicated digest for '{fullyQualifiedRepo}'");
                    }

                    yield return new DigestInfo(
                        DockerHelper.GetDigestSha(syndicatedDigest),
                        Options.RepoPrefix + syndicatedRepo,
                        syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags));
                }
            }

            foreach (PlatformData platform in image.Platforms)
            {
                string sha = DockerHelper.GetDigestSha(platform.Digest);

                yield return new DigestInfo(sha, Options.RepoPrefix + repo.Repo, platform.SimpleTags);

                // Find all syndicated simple tags grouped by their syndicated repo
                IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = (platform.PlatformInfo?.Tags ?? Enumerable.Empty<TagInfo>())
                    .Where(tagInfo => platform.SimpleTags.Contains(tagInfo.Name) && tagInfo.SyndicatedRepo != null)
                    .GroupBy(tagInfo => tagInfo.SyndicatedRepo);

                foreach (IGrouping<string, TagInfo> syndicatedTags in syndicatedTagGroups)
                {
                    string syndicatedRepo = syndicatedTags.Key;
                    yield return new DigestInfo(
                        sha,
                        Options.RepoPrefix + syndicatedRepo,
                        syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags));
                }
            }
        }
    }
}
#nullable disable
