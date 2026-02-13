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
    public class WaitForMarAnnotationIngestionCommand : Command<WaitForMarAnnotationIngestionOptions, WaitForMarAnnotationIngestionOptionsBuilder>
    {
        private readonly ILogger _loggerService;
        private readonly IMarImageIngestionReporter _imageIngestionReporter;

        public WaitForMarAnnotationIngestionCommand(
            ILogger loggerService, IMarImageIngestionReporter imageIngestionReporter)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _imageIngestionReporter = imageIngestionReporter ?? throw new ArgumentNullException(nameof(imageIngestionReporter));
        }

        protected override string Description => "Waits for annotations to complete ingestion into MAR";

        public override async Task ExecuteAsync()
        {
            _loggerService.LogInformation("WAITING FOR ANNOTATION INGESTION");

            string[] annotationDigests = File.ReadAllLines(Options.AnnotationDigestsPath);
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
