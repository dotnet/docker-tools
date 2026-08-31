// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMarAnnotationIngestionCommand : Command<WaitForMarAnnotationIngestionOptions>
    {
        private readonly ILogger<WaitForMarAnnotationIngestionCommand> _logger;
        private readonly IMarImageIngestionReporter _imageIngestionReporter;
        private readonly IArtifactService _artifactService;

        public WaitForMarAnnotationIngestionCommand(
            ILogger<WaitForMarAnnotationIngestionCommand> logger,
            IMarImageIngestionReporter imageIngestionReporter,
            IArtifactService artifactService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _imageIngestionReporter = imageIngestionReporter ?? throw new ArgumentNullException(nameof(imageIngestionReporter));
            _artifactService = artifactService ?? throw new ArgumentNullException(nameof(artifactService));
        }

        protected override string Description => "Waits for annotations to complete ingestion into MAR";

        public override async Task ExecuteAsync()
        {
            _logger.LogInformation("WAITING FOR ANNOTATION INGESTION");
            string annotationDigestsPath = _artifactService.ResolvePath(Options.AnnotationDigestsPath);

            string[] annotationDigests = File.ReadAllLines(annotationDigestsPath);
            IEnumerable<DigestInfo> digests = annotationDigests
                .Select(digest =>
                {
                    ImageName name = ImageName.Parse(digest);
                    if (name.Digest is null)
                    {
                        throw new Exception($"Could not parse digest SHA value from '{digest}'.");
                    }
                    return new DigestInfo(name.Digest, name.Repo, tags: []);
                });

            if (!Options.IsDryRun)
            {
                await _imageIngestionReporter.ReportImageStatusesAsync(
                    Options.MarServiceConnection,
                    digests,
                    Options.IngestionOptions.WaitTimeout,
                    Options.IngestionOptions.RequeryDelay,
                    minimumQueueTime: null);
            }
        }
    }
}
