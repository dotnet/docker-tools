// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestCommand : Command<PublishManifestOptions>
    {
        public PublishManifestCommand() : base()
        {
        }

        public override void Execute()
        {
            WriteHeading("GENERATING MANIFESTS");
            foreach (RepoInfo repo in Manifest.Repos)
            {
                foreach (ImageInfo image in repo.Images)
                {
                    foreach (TagInfo tag in image.SharedTags)
                    {
                        StringBuilder manifestYml = new StringBuilder();
                        manifestYml.AppendLine($"image: {tag.Name}");
                        manifestYml.AppendLine("manifests:");

                        foreach (PlatformInfo platform in image.Platforms)
                        {
                            manifestYml.AppendLine($"  -");
                            manifestYml.AppendLine($"    image: {platform.Tags.First().FullyQualifiedName}");
                            manifestYml.AppendLine($"    platform:");
                            manifestYml.AppendLine($"      architecture: {platform.Model.Architecture.ToString().ToLowerInvariant()}");
                            manifestYml.AppendLine($"      os: {platform.Model.OS}");
                            if (platform.Model.Variant != null)
                            {
                                manifestYml.AppendLine($"      variant: {platform.Model.Variant}");
                            }
                        }

                        Console.WriteLine($"-- PUBLISHING MANIFEST:{Environment.NewLine}{manifestYml}");
                        File.WriteAllText("manifest.yml", manifestYml.ToString());

                        // ExecuteWithRetry because the manifest-tool fails periodically with communicating
                        // with the Docker Registry.
                        ExecuteHelper.ExecuteWithRetry(
                            "manifest-tool",
                            $"--username {Options.Username} --password {Options.Password} push from-spec manifest.yml",
                            Options.IsDryRun);
                    }
                }
            }
        }
    }
}
