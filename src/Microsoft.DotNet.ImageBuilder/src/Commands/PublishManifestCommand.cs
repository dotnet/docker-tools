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
        private readonly IManifestToolService manifestToolService;
        private readonly IEnvironmentService environmentService;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public PublishManifestCommand(
            IManifestToolService manifestToolService,
            IEnvironmentService environmentService,
            ILoggerService loggerService)
        {
            this.manifestToolService = manifestToolService ?? throw new ArgumentNullException(nameof(manifestToolService));
            this.environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public override Task ExecuteAsync()
        {
            this.loggerService.WriteHeading("GENERATING MANIFESTS");

            ExecuteWithUser(() =>
            {
                IEnumerable<ImageInfo> multiArchImages = Manifest.GetFilteredImages()
                    .Where(image => image.SharedTags.Any())
                    .ToList();

                DateTime createdDate = DateTime.Now.ToUniversalTime();
                Parallel.ForEach(multiArchImages, image =>
                {
                    string manifest = GenerateManifest(image);

                    string manifestFilename = $"manifest.{Guid.NewGuid()}.yml";
                    this.loggerService.WriteSubheading($"PUBLISHING MANIFEST:  '{manifestFilename}'{Environment.NewLine}{manifest}");
                    File.WriteAllText(manifestFilename, manifest);

                    try
                    {
                        this.manifestToolService.PushFromSpec(manifestFilename, Options.IsDryRun);
                    }
                    finally
                    {
                        File.Delete(manifestFilename);
                    }
                });

                WriteManifestSummary(multiArchImages);

                SaveTagInfoToImageInfoFile(multiArchImages, createdDate);
            });

            return Task.CompletedTask;
        }

        private void SaveTagInfoToImageInfoFile(IEnumerable<ImageInfo> imageInfos, DateTime createdDate)
        {
            this.loggerService.WriteSubheading("SETTING TAG INFO");

            ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            // Find the images from the image info file that correspond to the images that were published
            IEnumerable<ImageData> imageDataList = imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images)
                .Where(image => imageInfos.Contains(image.ManifestImage));

            if (imageDataList.Count() != imageInfos.Count())
            {
                this.loggerService.WriteError(
                    $"There is a mismatch between the number of images being published and the number of images contained in the image info file ({imageInfos.Count()} vs {imageDataList.Count()}, respectively).");
                this.environmentService.Exit(1);
            }

            IEnumerable<SharedTag> sharedTags = imageDataList
                .SelectMany(image => image.SharedTags.OrderBy(tag => tag.Name));

            IEnumerable<TagInfo> tagInfos = imageDataList
                .SelectMany(image => image.ManifestImage.SharedTags.OrderBy(tag => tag.Name));

            // Generate a set of tuples that pairs each SharedTag with its corresponding TagInfo
            IEnumerable<(SharedTag SharedTag, TagInfo TagInfo)> sharedTagInfos = sharedTags.Zip(tagInfos);

            foreach ((SharedTag SharedTag, TagInfo TagInfo) sharedTagInfo in sharedTagInfos)
            {
                sharedTagInfo.SharedTag.Created = createdDate;
            }

            string imageInfoString = JsonHelper.SerializeObject(imageArtifactDetails);
            File.WriteAllText(Options.ImageInfoPath, imageInfoString);
        }

        private string GenerateManifest(ImageInfo image)
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
                manifestYml.AppendLine($"- image: {platform.Tags.First().FullyQualifiedName}");
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

        private void WriteManifestSummary(IEnumerable<ImageInfo> multiArchImages)
        {
            this.loggerService.WriteHeading("MANIFEST TAGS PUBLISHED");

            IEnumerable<string> multiArchTags = multiArchImages.SelectMany(image => image.SharedTags)
                .Select(tag => tag.FullyQualifiedName)
                .ToArray();
            if (multiArchTags.Any())
            {
                foreach (string tag in multiArchTags)
                {
                    this.loggerService.WriteMessage(tag);
                }
            }
            else
            {
                this.loggerService.WriteMessage("No manifests published");
            }

            this.loggerService.WriteMessage();
        }
    }
}
