// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GetBaseImageStatusCommand : ManifestCommand<GetBaseImageStatusOptions, GetBaseImageStatusOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public GetBaseImageStatusCommand(IDockerService dockerService, ILoggerService loggerService)
        {
            _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Displays the status of the referenced external base images";

        public override async Task ExecuteAsync()
        {
            if (Options.ContinuousMode)
            {
                while (true)
                {
                    CheckStatus();
                    await Task.Delay(Options.ContinuousModeDelay);
                }
            }
            else
            {
                CheckStatus();
            }
        }

        private void CheckStatus()
        {
            IEnumerable<(string Tag, string Platform)> platformTags = Manifest.GetFilteredPlatforms()
                .Select(platform =>
                {
                    // Find the last FROM image that's an external image. This is not the same as the last
                    // ExternalFromImage because that could be the first FROM listed in the Dockerfile which is
                    // not what we want.
                    string? tag = platform.FinalStageFromImage is not null && platform.IsInternalFromImage(platform.FinalStageFromImage) ?
                        null : platform.FinalStageFromImage;
                    return (Tag: tag, Platform: platform.PlatformLabel);
                })
                .Where(image => image.Tag != null)
                .Cast<(string, string)>()
                .Distinct()
                .ToList();

            _loggerService.WriteHeading("PULLING LATEST BASE IMAGES");
            foreach ((string Tag, string Platform) imageTag in platformTags)
            {
                _dockerService.PullImage(imageTag.Tag, imageTag.Platform, Options.IsDryRun);
            }

            _loggerService.WriteHeading("QUERYING STATUS");
            var statuses = platformTags
                .Select(imageTag => new
                {
                    imageTag.Tag,
                    DateCreated = _dockerService.GetCreatedDate(imageTag.Tag, Options.IsDryRun)
                })
                .ToList();

            _loggerService.WriteHeading("BASE IMAGE STATUS SUMMARY");
            foreach (var status in statuses)
            {
                TimeSpan timeDiff = DateTime.Now - status.DateCreated;

                string days = string.Empty;
                // Use TotalDays so that years are represented in terms of days
                int totalDays = (int)timeDiff.TotalDays;
                if (totalDays > 0)
                {
                    days = $"{totalDays} days, ";
                }

                _loggerService.WriteSubheading(status.Tag);
                _loggerService.WriteMessage($"Created {days}{timeDiff.Minutes} minutes ago");
                _loggerService.WriteMessage();
            }
        }
    }
}
#nullable disable
