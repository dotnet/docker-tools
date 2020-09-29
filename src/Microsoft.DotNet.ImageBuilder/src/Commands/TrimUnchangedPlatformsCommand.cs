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
    public class TrimUnchangedPlatformsCommand : Command<TrimUnchangedPlatformsOptions>
    {
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public TrimUnchangedPlatformsCommand(ILoggerService loggerService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public override async Task ExecuteAsync()
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
                            _loggerService.WriteMessage($"Removing unchanged platform '{platform.GetIdentifier()}'");
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
