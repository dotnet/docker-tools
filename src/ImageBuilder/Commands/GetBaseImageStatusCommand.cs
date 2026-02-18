// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetBaseImageStatusCommand : ManifestCommand<GetBaseImageStatusOptions, GetBaseImageStatusOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILogger<GetBaseImageStatusCommand> _logger;

        public GetBaseImageStatusCommand(IDockerService dockerService, ILogger<GetBaseImageStatusCommand> logger)
        {
            _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            _logger.LogInformation("PULLING LATEST BASE IMAGES");
            foreach ((string Tag, string Platform) imageTag in platformTags)
            {
                _dockerService.PullImage(imageTag.Tag, imageTag.Platform, Options.IsDryRun);
            }

            _logger.LogInformation("QUERYING STATUS");
            var statuses = platformTags
                .Select(imageTag => new
                {
                    Tag = imageTag.Tag,
                    DateCreated = _dockerService.GetCreatedDate(imageTag.Tag, Options.IsDryRun)
                })
                .ToList();

            _logger.LogInformation("BASE IMAGE STATUS SUMMARY");
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

                _logger.LogInformation(status.Tag);
                _logger.LogInformation($"Created {days}{timeDiff.Minutes} minutes ago");
                _logger.LogInformation(string.Empty);
            }
        }
    }
}
