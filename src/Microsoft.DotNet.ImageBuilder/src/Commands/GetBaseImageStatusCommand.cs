// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GetBaseImageStatusCommand : ManifestCommand<GetBaseImageStatusOptions>
    {
        private readonly IDockerService dockerService;
        private readonly ILoggerService loggerService;

        [ImportingConstructor]
        public GetBaseImageStatusCommand(IDockerService dockerService, ILoggerService loggerService)
        {
            this.dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        public override Task ExecuteAsync()
        {
            IEnumerable<string> imageTags = Manifest.GetFilteredPlatforms()
                .Select(platform =>
                {
                    // Find the last FROM image that's an external image. This is not the same as the last
                    // ExternalFromImage because that could be the first FROM listed in the Dockerfile which is
                    // not what we want.
                    string finalFromImage = platform.FromImages.Last();
                    return platform.IsInternalFromImage(finalFromImage) ? null : finalFromImage;
                })
                .Where(image => image != null)
                .Distinct()
                .ToList();

            this.loggerService.WriteHeading("PULLING LATEST BASE IMAGES");
            foreach (string imageTag in imageTags)
            {
                this.dockerService.PullImage(imageTag, Options.IsDryRun);
            }

            this.loggerService.WriteHeading("QUERYING STATUS");
            var statuses = imageTags
                .Select(tag => new
                {
                    Tag = tag,
                    DateCreated = this.dockerService.GetCreatedDate(tag, Options.IsDryRun)
                })
                .ToList();

            this.loggerService.WriteHeading("BASE IMAGE STATUS SUMMARY");
            foreach (var status in statuses)
            {
                TimeSpan timeDiff = DateTime.Now - status.DateCreated;

                string days = String.Empty;
                // Use TotalDays so that years are represented in terms of days
                int totalDays = (int)timeDiff.TotalDays;
                if (totalDays > 0)
                {
                    days = $"{totalDays} days, ";
                }

                this.loggerService.WriteSubheading(status.Tag);
                this.loggerService.WriteMessage($"Created {days}{timeDiff.Minutes} minutes ago");
                this.loggerService.WriteMessage();
            }

            return Task.CompletedTask;
        }
    }
}
