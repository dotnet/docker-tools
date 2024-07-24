// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class WaitForMarAnnotationIngestionCommand : Command<WaitForMarAnnotationIngestionOptions, WaitForMarAnnotationIngestionOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;
        private readonly IMarImageIngestionReporter _imageIngestionReporter;

        [ImportingConstructor]
        public WaitForMarAnnotationIngestionCommand(
            ILoggerService loggerService, IMarImageIngestionReporter imageIngestionReporter)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _imageIngestionReporter = imageIngestionReporter ?? throw new ArgumentNullException(nameof(imageIngestionReporter));
        }

        protected override string Description => "Waits for annotations to complete ingestion into MAR";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("WAITING FOR ANNOTATION INGESTION");

            EolAnnotationsData annotationsData = AnnotateEolDigestsCommand.LoadEolAnnotationsData(Options.EolDigestsListPath);
            IEnumerable<DigestInfo> digests = annotationsData.EolDigests
                .Select(annotation =>
                {
                    ImageName name = ImageName.Parse(annotation.Digest);
                    if (name.Digest is null)
                    {
                        throw new Exception($"Could not parse digest SHA value from '{annotation.Digest}'.");
                    }
                    return new DigestInfo(name.Digest, name.Repo, tags: []);
                });

            if (!Options.IsDryRun)
            {
                await _imageIngestionReporter.ReportImageStatusesAsync(digests, Options.IngestionOptions.WaitTimeout, Options.IngestionOptions.RequeryDelay, minimumQueueTime: null);
            }
        }
    }
}
