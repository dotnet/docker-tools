// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestCommand : Command<PublishManifestOptions>
    {
        public PublishManifestCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Utilities.WriteHeading("GENERATING MANIFESTS");

            DockerHelper.Login(Options.Username, Options.Password, Options.Server, Options.IsDryRun);
            try
            {
                IEnumerable<ImageInfo> multiArchImages = Manifest.Repos
                    .SelectMany(repo => repo.Images)
                    .Where(image => image.SharedTags.Any());
                foreach (ImageInfo image in multiArchImages)
                {
                    string manifest = GenerateManifest(image);

                    Console.WriteLine($"-- PUBLISHING MANIFEST:{Environment.NewLine}{manifest}");
                    File.WriteAllText("manifest.yml", manifest);

                    // ExecuteWithRetry because the manifest-tool fails periodically with communicating
                    // with the Docker Registry.
                    ExecuteHelper.ExecuteWithRetry("manifest-tool", "push from-spec manifest.yml", Options.IsDryRun);
                }
            }
            finally
            {
                DockerHelper.Logout(Options.Server, Options.IsDryRun);
            }

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
            foreach (PlatformInfo platform in image.Platforms)
            {
                manifestYml.AppendLine($"  -");
                manifestYml.AppendLine($"    image: {platform.Tags.First().FullyQualifiedName}");
                manifestYml.AppendLine($"    platform:");
                manifestYml.AppendLine($"      architecture: {platform.Model.Architecture.ToString().ToLowerInvariant()}");
                manifestYml.AppendLine($"      os: {platform.Model.OS.ToString().ToLowerInvariant()}");
                if (platform.Model.Variant != null)
                {
                    manifestYml.AppendLine($"      variant: {platform.Model.Variant}");
                }
            }

            return manifestYml.ToString();
        }
    }
}
