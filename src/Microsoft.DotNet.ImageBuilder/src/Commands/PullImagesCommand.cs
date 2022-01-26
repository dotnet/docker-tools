// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
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
            IEnumerable<(string Tag, string Platform)> platformTags = Manifest.GetFilteredPlatforms()
                .Where(platform => platform.Tags.Any())
                .Select(platform => (platform.Tags.First().FullyQualifiedName, platform.PlatformLabel))
                .Distinct()
                .ToList();

            _loggerService.WriteHeading("PULLING IMAGES");
            foreach ((string Tag, string Platform) platformTag in platformTags)
            {
                _dockerService.PullImage(platformTag.Tag, platformTag.Platform, Options.IsDryRun);
            }

            if (Options.OutputVariableName is not null)
            {
                _loggerService.WriteMessage(PipelineHelper.FormatOutputVariable(Options.OutputVariableName, string.Join(',', platformTags)));
            }

            return Task.CompletedTask;
        }
    }
}
#nullable disable
