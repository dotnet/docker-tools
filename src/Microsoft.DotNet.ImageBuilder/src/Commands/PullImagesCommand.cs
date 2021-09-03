// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PullImagesCommand : ManifestCommand<PullImagesOptions, PullImagesOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILoggerService _loggerService;

        [ImportingConstructor]
        public PullImagesCommand(IDockerService dockerService, ILoggerService loggerService)
        {
            _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
        }

        protected override string Description => "Pulls the images described in the manifest";

        public override Task ExecuteAsync()
        {
            IEnumerable<string> imageTags = Manifest.GetFilteredPlatforms()
                .Where(platform => platform.Tags.Any())
                .Select(platform => platform.Tags.First().FullyQualifiedName)
                .ToList();

            StringBuilder pulledImages = new();

            _loggerService.WriteHeading("PULLING IMAGES");
            foreach (string imageTag in imageTags)
            {
                _dockerService.PullImage(imageTag, Options.IsDryRun);

                if (pulledImages.Length > 0)
                {
                    pulledImages.Append(',');
                }

                pulledImages.Append(imageTag);
            }

            if (Options.OutputVariableName is not null)
            {
                _loggerService.WriteMessage(PipelineHelper.FormatOutputVariable(Options.OutputVariableName, pulledImages.ToString()));
            }

            return Task.CompletedTask;
        }
    }
}
#nullable disable
