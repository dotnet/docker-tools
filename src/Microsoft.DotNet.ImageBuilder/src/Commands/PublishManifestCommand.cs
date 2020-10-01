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

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishManifestCommand : DockerRegistryCommand<PublishManifestOptions>
    {
        private readonly IManifestToolService _manifestToolService;
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public PublishManifestCommand(
            IManifestToolService manifestToolService,
            ILoggerService loggerService)
        {
            _manifestToolService = manifestToolService ?? throw new ArgumentNullException(nameof(manifestToolService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public override Task ExecuteAsync()
        {
            _loggerService.WriteHeading("GENERATING MANIFESTS");

            ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            ExecuteWithUser(() =>
            {
                IEnumerable<(RepoInfo Repo, ImageInfo Image)> multiArchRepoImages = Manifest.FilteredRepos
                    .SelectMany(repo =>
                        repo.FilteredImages
                            .Where(image => image.SharedTags.Any())
                            .Select(image => (repo, image)))
                    .ToList();

                DateTime createdDate = DateTime.Now.ToUniversalTime();
                Parallel.ForEach(multiArchRepoImages, repoImage =>
                {
                    string manifest = GenerateManifest(repoImage.Repo, repoImage.Image);

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

                WriteManifestSummary(imageArtifactDetails);

                SaveTagInfoToImageInfoFile(createdDate, imageArtifactDetails);
            });

            return Task.CompletedTask;
        }

        private void SaveTagInfoToImageInfoFile(DateTime createdDate, ImageArtifactDetails imageArtifactDetails)
        {
            _loggerService.WriteSubheading("SETTING TAG INFO");

            IEnumerable<ImageData> images = imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images)
                .Where(image => image.Manifest != null);

            Parallel.ForEach(images, image =>
            {
                image.Manifest.Created = createdDate;

                TagInfo sharedTag = image.ManifestImage.SharedTags.First();
                image.Manifest.Digest = DockerHelper.GetDigestString(
                    image.ManifestRepo.FullModelName,
                    _manifestToolService.GetManifestDigestSha(
                        ManifestMediaType.ManifestList, sharedTag.FullyQualifiedName, Options.IsDryRun));
            });

            string imageInfoString = JsonHelper.SerializeObject(imageArtifactDetails);
            File.WriteAllText(Options.ImageInfoPath, imageInfoString);
        }

        private string GenerateManifest(RepoInfo repo, ImageInfo image)
        {
            StringBuilder manifestYml = new StringBuilder();
            manifestYml.AppendLine($"image: {image.SharedTags.First().FullyQualifiedName}");

            IEnumerable<string> additionalTags = image.SharedTags
                .Select(tag => tag.Name)
                .Skip(1);
            if (additionalTags.Any())
            {
                manifestYml.AppendLine($"tags: [{string.Join(",", additionalTags)}]");
            }

            manifestYml.AppendLine("manifests:");
            foreach (PlatformInfo platform in image.AllPlatforms)
            {
                string imageTag;
                if (platform.Tags.Any())
                {
                    imageTag = platform.Tags.First().FullyQualifiedName;
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

                    imageTag = matchingImagePlatform.Platform.Tags.First().FullyQualifiedName;
                }

                manifestYml.AppendLine($"- image: {imageTag}");
                manifestYml.AppendLine($"  platform:");
                manifestYml.AppendLine($"    architecture: {platform.Model.Architecture.GetDockerName()}");
                manifestYml.AppendLine($"    os: {platform.Model.OS.GetDockerName()}");
                if (platform.Model.Variant != null)
                {
                    manifestYml.AppendLine($"    variant: {platform.Model.Variant}");
                }
            }

            return manifestYml.ToString();
        }

        private void WriteManifestSummary(ImageArtifactDetails imageArtifactDetails)
        {
            _loggerService.WriteHeading("MANIFEST TAGS PUBLISHED");

            IEnumerable<string> multiArchTags = imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images)
                .SelectMany(image => image.ManifestImage.SharedTags)
                .Select(tag => tag.FullyQualifiedName)
                .ToArray();
            if (multiArchTags.Any())
            {
                foreach (string tag in multiArchTags)
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
