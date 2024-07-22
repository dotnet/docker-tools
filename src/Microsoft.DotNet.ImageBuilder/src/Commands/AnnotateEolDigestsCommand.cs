// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class AnnotateEolDigestsCommand : Command<AnnotateEolDigestsOptions, AnnotateEolDigestsOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;
        private readonly IOrasService _orasService;
        private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
        private readonly ConcurrentBag<EolDigestData> _failedAnnotations = [];
        private readonly ConcurrentBag<EolDigestData> _skippedAnnotations = [];
        private readonly ConcurrentBag<EolDigestData> _existingAnnotations = [];

        [ImportingConstructor]
        public AnnotateEolDigestsCommand(
            ILoggerService loggerService,
            IOrasService orasService,
            IRegistryCredentialsProvider registryCredentialsProvider)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _orasService = orasService ?? throw new ArgumentNullException(nameof(orasService));
            _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
        }

        protected override string Description => "Annotates EOL digests in Docker Registry";

        public override async Task ExecuteAsync()
        {
            EolAnnotationsData eolAnnotations = LoadEolAnnotationsData(Options.EolDigestsListOutputPath);
            DateOnly? globalEolDate = eolAnnotations.EolDate;

            await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
                Options.IsDryRun,
                () =>
                {
                    Parallel.ForEach(eolAnnotations.EolDigests, digestData =>
                    {
                        AnnotateDigest(digestData, globalEolDate);
                    });

                    return Task.CompletedTask;
                },
                Options.CredentialsOptions,
                registryName: Options.AcrName,
                ownedAcr: Options.AcrName);

            WriteNonEmptySummary(_skippedAnnotations,
                "The following digests were skipped because they have existing annotations with matching EOL dates.");

            WriteNonEmptySummary(_existingAnnotations,
                "The following digests were skipped because they have existing annotations with non-matching EOL dates. These need to be deleted from MAR before they can be re-annotated.");

            WriteNonEmptySummary(_failedAnnotations,
                "The following digests had annotation failures:");

            if (!_existingAnnotations.IsEmpty || !_failedAnnotations.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Some digest annotations failed or were skipped due to existing non-matching EOL date annotations (failed: {_failedAnnotations.Count}, skipped: {_existingAnnotations.Count}).");
            }
        }

        private void WriteNonEmptySummary(ConcurrentBag<EolDigestData> eolDigests, string message)
        {
            if (!eolDigests.IsEmpty)
            {
                _loggerService.WriteMessage(message);
                _loggerService.WriteMessage("");
                _loggerService.WriteMessage(JsonConvert.SerializeObject(new EolAnnotationsData(eolDigests: [.. eolDigests])));
                _loggerService.WriteMessage("");
            }
        }

        private void AnnotateDigest(EolDigestData digestData, DateOnly? globalEolDate)
        {
            DateOnly? eolDate = digestData.EolDate ?? globalEolDate;
            if (eolDate is null)
            {
                _failedAnnotations.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate });
                _loggerService.WriteError($"EOL date is not specified for digest '{digestData.Digest}'.");
                return;
            }

            if (!_orasService.IsDigestAnnotatedForEol(digestData.Digest, _loggerService, Options.IsDryRun, out OciManifest? lifecycleArtifactManifest))
            {
                _loggerService.WriteMessage($"Annotating EOL for digest '{digestData.Digest}', date '{eolDate}'");
                if (!_orasService.AnnotateEolDigest(digestData.Digest, eolDate.Value, _loggerService, Options.IsDryRun))
                {
                    // We will capture all failures and log the json data at the end.
                    // Json data can be used to rerun the failed annotations.
                    _failedAnnotations.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate, Tag = digestData.Tag });
                }
            }
            else
            {
                if (lifecycleArtifactManifest.Annotations[OrasService.EndOfLifeAnnotation] == eolDate?.ToString(OrasService.EolDateFormat))
                {
                    _loggerService.WriteMessage($"Skipping digest '{digestData.Digest}' because it is already annotated with a matching EOL date.");
                    _skippedAnnotations.Add(digestData);
                }
                else
                {
                    _loggerService.WriteError($"Could not annotate digest '{digestData.Digest}' because it has an existing non-matching EOL date: {eolDate}.");
                    _existingAnnotations.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate });
                }
            }
        }

        private static EolAnnotationsData LoadEolAnnotationsData(string eolDigestsListPath)
        {
            string eolAnnotationsJson = File.ReadAllText(eolDigestsListPath);
            EolAnnotationsData? eolAnnotations = JsonConvert.DeserializeObject<EolAnnotationsData>(eolAnnotationsJson);
            return eolAnnotations is null
                ? throw new JsonException($"Unable to correctly deserialize path '{eolAnnotationsJson}'.")
                : eolAnnotations;
        }
    }
}
