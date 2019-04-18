// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestCommand : DockerRegistryCommand<PublishManifestOptions>
    {
        public PublishManifestCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING MANIFESTS");

            ExecuteWithUser(() =>
            {
                IEnumerable<ImageInfo> multiArchImages = Manifest.FilteredRepos
                    .SelectMany(repo => repo.AllImages)
                    .Where(image => image.SharedTags.Any());
                Parallel.ForEach(multiArchImages, image =>
                {
                    string manifest = GenerateManifest(image);

                    string manifestFilename = $"manifest.{Guid.NewGuid()}.yml";
                    Logger.WriteSubheading($"PUBLISHING MANIFEST:  '{manifestFilename}'{Environment.NewLine}{manifest}");
                    File.WriteAllText(manifestFilename, manifest);

                    try
                    {
                        // ExecuteWithRetry because the manifest-tool fails periodically while communicating
                        // with the Docker Registry.
                        ExecuteHelper.ExecuteWithRetry("manifest-tool", $"push from-spec {manifestFilename}", Options.IsDryRun);
                    }
                    finally
                    {
                        File.Delete(manifestFilename);
                    }
                });

                WriteManifestSummary(multiArchImages);
            });

            return Task.CompletedTask;
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
            Logger.WriteHeading("MANIFEST TAGS PUBLISHED");

            IEnumerable<string> multiArchTags = multiArchImages.SelectMany(image => image.SharedTags)
                .Select(tag => tag.FullyQualifiedName)
                .ToArray();
            if (multiArchTags.Any())
            {
                foreach (string tag in multiArchTags)
                {
                    Logger.WriteMessage(tag);
                }
            }
            else
            {
                Logger.WriteMessage("No manifests published");
            }

            Logger.WriteMessage();
        }
    }
}
