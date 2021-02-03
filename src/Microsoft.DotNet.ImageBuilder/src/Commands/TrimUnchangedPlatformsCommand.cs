// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class TrimUnchangedPlatformsCommand : Command<TrimUnchangedPlatformsOptions, TrimUnchangedPlatformsOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public TrimUnchangedPlatformsCommand(IDockerService dockerService, ILoggerService loggerService)
            : base(dockerService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Trims platforms marked as unchanged from the image info file";

        protected override async Task ExecuteCoreAsync()
        {
            _loggerService.WriteHeading("TRIMMING UNCHANGED PLATFORMS");

            string imageInfoContents = await File.ReadAllTextAsync(Options.ImageInfoPath);
            ImageArtifactDetails imageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(imageInfoContents);
            RemoveUnchangedPlatforms(imageArtifactDetails);
            imageInfoContents = JsonHelper.SerializeObject(imageArtifactDetails);

            if (!Options.IsDryRun)
            {
                await File.WriteAllTextAsync(Options.ImageInfoPath, imageInfoContents);
            }
        }

        private void RemoveUnchangedPlatforms(ImageArtifactDetails imageArtifactDetails)
        {
            for (int repoIndex = imageArtifactDetails.Repos.Count - 1; repoIndex >= 0; repoIndex--)
            {
                RepoData repo = imageArtifactDetails.Repos[repoIndex];
                for (int imageIndex = repo.Images.Count - 1; imageIndex >= 0; imageIndex--)
                {
                    ImageData image = repo.Images[imageIndex];
                    for (int i = image.Platforms.Count - 1; i >= 0; i--)
                    {
                        PlatformData platform = image.Platforms[i];
                        if (platform.IsUnchanged)
                        {
                            // Exclude the ProductVersion from the identifier because it requires the ImageInfo to be set on the platform.
                            // But it's not set because we haven't used ImageInfoHelper to load it, we've just deserialized it directly.
                            // Using ImageInfoHelper requires having the manifest but that seems unnecessary since it's not needed for the logic
                            // of this command other than this simple logging statement.
                            _loggerService.WriteMessage($"Removing unchanged platform '{platform.GetIdentifier(excludeProductVersion: true)}'");
                            image.Platforms.Remove(platform);
                        }
                    }

                    if (!image.Platforms.Any())
                    {
                        repo.Images.Remove(image);
                    }
                }

                if (!repo.Images.Any())
                {
                    imageArtifactDetails.Repos.Remove(repo);
                }
            }
        }
    }
}
