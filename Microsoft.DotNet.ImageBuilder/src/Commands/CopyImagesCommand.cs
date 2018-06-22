// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class CopyImagesCommand : Command<CopyImagesOptions>
    {
        public CopyImagesCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            Logger.WriteHeading("COPING IMAGES");

            IEnumerable<TagInfo> platformTags = Manifest.ActiveImages
                    .SelectMany(image => image.ActivePlatforms)
                    .SelectMany(platform => platform.Tags)
                    .ToArray();

            Logger.WriteHeading("PULLING SOURCE IMAGES");
            DockerHelper.ExecuteWithUser(
                () =>
                {
                    foreach (TagInfo platformTag in platformTags)
                    {
                        string sourceImage = $"{Options.SourceRepo}:{platformTag.Name}";
                        ExecuteHelper.ExecuteWithRetry("docker", $"pull {sourceImage}", Options.IsDryRun);
                        ExecuteHelper.ExecuteWithRetry("docker", $"tag {sourceImage} {platformTag.FullyQualifiedName}", Options.IsDryRun);
                    }
                },
                Options.SourceUsername,
                Options.SourcePassword,
                Options.SourceServer,
                Options.IsDryRun);

            Logger.WriteHeading("PUSHING IMAGES TO DESTINATION");
            DockerHelper.ExecuteWithUser(
                () =>
                {
                    foreach (TagInfo platformTag in platformTags)
                    {
                        ExecuteHelper.ExecuteWithRetry("docker", $"push {platformTag.FullyQualifiedName}", Options.IsDryRun);
                    }
                },
                Options.DestinationUsername,
                Options.DestinationPassword,
                Options.DestinationServer,
                Options.IsDryRun);

            return Task.CompletedTask;
        }
    }
}
