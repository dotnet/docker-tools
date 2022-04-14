// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishManifestCommand : DockerRegistryCommand<PublishManifestOptions, PublishManifestOptionsBuilder>
    {
        private readonly IManifestToolService _manifestToolService;
        private readonly ILoggerService _loggerService;
        private readonly IDateTimeService _dateTimeService;
        private List<string> _publishedManifestTags = new List<string>();

        [ImportingConstructor]
        public PublishManifestCommand(
            IManifestToolService manifestToolService,
            ILoggerService loggerService,
            IDateTimeService dateTimeService)
        {
            _manifestToolService = manifestToolService ?? throw new ArgumentNullException(nameof(manifestToolService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
        }

        protected override string Description => "Creates and publishes the manifest to the Docker Registry";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("GENERATING MANIFESTS");

            ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            await ExecuteWithUserAsync(async () =>
            {
                IEnumerable<string> manifests = Manifest.FilteredRepos
                    .SelectMany(repo =>
                        repo.FilteredImages
                            .Where(image => image.SharedTags.Any())
                            .Select(image => (repo, image)))
                    .SelectMany(repoImage => GenerateManifests(repoImage.repo, repoImage.image))
                    .ToList();

                DateTime createdDate = _dateTimeService.UtcNow;
                Parallel.ForEach(manifests, manifest =>
                {
                    string manifestFilename = $"manifest.{Guid.NewGuid()}.yml";
                    _loggerService.WriteSubheading($"PUBLISHING MANIFEST:  '{manifestFilename}'{Environment.NewLine}{manifest}");
                    File.WriteAllText(manifestFilename, manifest);

                    try
                    {
                        _manifestToolService.PushFromSpec(manifestFilename, Options.IsDryRun);
                    }
                    finally
                    {
                        File.Delete(manifestFilename);
                    }
                });

                WriteManifestSummary();

                await SaveTagInfoToImageInfoFileAsync(createdDate, imageArtifactDetails);
            });
        }

        private async Task SaveTagInfoToImageInfoFileAsync(DateTime createdDate, ImageArtifactDetails imageArtifactDetails)
        {
            _loggerService.WriteSubheading("SETTING TAG INFO");

            IEnumerable<ImageData> images = imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images)
                .Where(image => image.Manifest != null);

            await Parallel.ForEachAsync(images, async (image, cancellationToken) =>
            {
                image.Manifest.Created = createdDate;

                TagInfo sharedTag = image.ManifestImage.SharedTags.First();

                image.Manifest.Digest = DockerHelper.GetDigestString(
                    image.ManifestRepo.FullModelName,
                    await _manifestToolService.GetManifestDigestShaAsync(
                        ManifestMediaType.ManifestList, sharedTag.FullyQualifiedName, Options.IsDryRun));

                IEnumerable<(string Repo, string Tag)> syndicatedRepresentativeSharedTags = image.ManifestImage.SharedTags
                    .Where(tag => tag.SyndicatedRepo is not null)
                    .GroupBy(tag => tag.SyndicatedRepo)
                    .Select(group => (group.Key, group.First().SyndicatedDestinationTags.First()))
                    .Cast<(string Repo, string Tag)>()
                    .OrderBy(obj => obj.Repo)
                    .ThenBy(obj => obj.Tag);

                foreach ((string Repo, string Tag) syndicatedSharedTag in syndicatedRepresentativeSharedTags)
                {
                    string digest = DockerHelper.GetDigestString(
                        DockerHelper.GetImageName(Manifest.Model.Registry, syndicatedSharedTag.Repo),
                        await _manifestToolService.GetManifestDigestShaAsync(
                            ManifestMediaType.ManifestList,
                            DockerHelper.GetImageName(Manifest.Registry, Options.RepoPrefix + syndicatedSharedTag.Repo, syndicatedSharedTag.Tag),
                            Options.IsDryRun));
                    image.Manifest.SyndicatedDigests.Add(digest);
                }
            });

            string imageInfoString = JsonHelper.SerializeObject(imageArtifactDetails);
            File.WriteAllText(Options.ImageInfoPath, imageInfoString);
        }

        private IEnumerable<string> GenerateManifests(RepoInfo repo, ImageInfo image)
        {
            yield return GenerateManifest(repo, image, image.SharedTags.Select(tag => tag.Name),
                tag => DockerHelper.GetImageName(Manifest.Registry, Options.RepoPrefix + repo.Name, tag),
                platform => platform.Tags.First());

            IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = image.SharedTags
                .Where(tag => tag.SyndicatedRepo != null)
                .GroupBy(tag => tag.SyndicatedRepo);

            foreach (IGrouping<string, TagInfo> syndicatedTags in syndicatedTagGroups)
            {
                string syndicatedRepo = syndicatedTags.Key;
                IEnumerable<string> destinationTags = syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags);

                // There won't always be a platform tag that's syndicated. So if a manifest tag is syndicated, we need to account
                // for the possibility that a given platform for that manifest will not have a matching syndicated repo.

                yield return GenerateManifest(repo, image, destinationTags,
                    tag => DockerHelper.GetImageName(Manifest.Registry, Options.RepoPrefix + syndicatedRepo, tag),
                    platform => platform.Tags.FirstOrDefault(tag => tag.SyndicatedRepo == syndicatedRepo));
            }
        }

        private string GenerateManifest(RepoInfo repo, ImageInfo image, IEnumerable<string> tags, Func<string, string> getImageName,
            Func<PlatformInfo, TagInfo?> getTagRepresentative)
        {
            string imageName = getImageName(tags.First());
            StringBuilder manifestYml = new();
            manifestYml.AppendLine($"image: {imageName}");
            _publishedManifestTags.Add(imageName);

            string repoName = DockerHelper.GetRepo(imageName);

            IEnumerable<string> additionalTags = tags.Skip(1);

            if (additionalTags.Any())
            {
                manifestYml.AppendLine($"tags: [{string.Join(",", additionalTags)}]");
            }

            _publishedManifestTags.AddRange(additionalTags.Select(tag => $"{repoName}:{tag}"));

            manifestYml.AppendLine("manifests:");
            foreach (PlatformInfo platform in image.AllPlatforms)
            {
                TagInfo? imageTag;
                if (platform.Tags.Any())
                {
                    imageTag = getTagRepresentative(platform);
                }
                else
                {
                    (ImageInfo Image, PlatformInfo Platform) matchingImagePlatform = repo.AllImages
                        .SelectMany(image =>
                            image.AllPlatforms
                                .Select(p => (Image: image, Platform: p))
                                .Where(imagePlatform => platform != imagePlatform.Platform &&
                                    PlatformInfo.AreMatchingPlatforms(image, platform, imagePlatform.Image, imagePlatform.Platform) &&
                                    imagePlatform.Platform.Tags.Any()))
                        .FirstOrDefault();

                    if (matchingImagePlatform.Platform is null)
                    {
                        throw new InvalidOperationException(
                            $"Could not find a platform with concrete tags for '{platform.DockerfilePathRelativeToManifest}'.");
                    }

                    imageTag = getTagRepresentative(matchingImagePlatform.Platform);
                }

                if (imageTag is not null)
                {
                    manifestYml.AppendLine($"- image: {getImageName(imageTag.Name)}");
                    manifestYml.AppendLine($"  platform:");
                    manifestYml.AppendLine($"    architecture: {platform.Model.Architecture.GetDockerName()}");
                    manifestYml.AppendLine($"    os: {platform.Model.OS.GetDockerName()}");
                    if (platform.Model.Variant != null)
                    {
                        manifestYml.AppendLine($"    variant: {platform.Model.Variant}");
                    }
                }
            }

            return manifestYml.ToString();
        }

        private void WriteManifestSummary()
        {
            _loggerService.WriteHeading("MANIFEST TAGS PUBLISHED");

            if (_publishedManifestTags.Any())
            {
                foreach (string tag in _publishedManifestTags)
                {
                    _loggerService.WriteMessage(tag);
                }
            }
            else
            {
                _loggerService.WriteMessage("No manifests published");
            }

            _loggerService.WriteMessage();
        }
    }
}
#nullable disable
