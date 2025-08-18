// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.MarBulkDeletion;
using Microsoft.DotNet.ImageBuilder.Models.Oci;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class AnnotateEolDigestsCommand : Command<AnnotateEolDigestsOptions, AnnotateEolDigestsOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;
        private readonly ILifecycleMetadataService _lifecycleMetadataService;
        private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
        private readonly ConcurrentBag<EolDigestData> _failedAnnotationImageDigests = [];
        private readonly ConcurrentBag<EolDigestData> _skippedAnnotationImageDigests = [];
        private readonly ConcurrentBag<EolDigestData> _existingAnnotationImageDigests = [];
        private readonly ConcurrentBag<string> _existingAnnotationDigests = [];
        private readonly ConcurrentBag<string> _createdAnnotationDigests = [];

        private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        [ImportingConstructor]
        public AnnotateEolDigestsCommand(
            ILoggerService loggerService,
            ILifecycleMetadataService lifecycleMetadataService,
            IRegistryCredentialsProvider registryCredentialsProvider)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _lifecycleMetadataService = lifecycleMetadataService ?? throw new ArgumentNullException(nameof(lifecycleMetadataService));
            _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
        }

        protected override string Description => "Annotates EOL digests in Docker Registry";

        public override async Task ExecuteAsync()
        {
            EolAnnotationsData eolAnnotations = LoadEolAnnotationsData(Options.EolDigestsListPath);
            DateOnly? globalEolDate = eolAnnotations.EolDate;

            await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
                Options.IsDryRun,
                () =>
                {
                    Parallel.ForEach(eolAnnotations.EolDigests, digestData => AnnotateDigest(digestData, globalEolDate));
                    return Task.CompletedTask;
                },
                Options.CredentialsOptions,
                registryName: Options.AcrName,
                ownedAcr: Options.AcrName,
                serviceConnection: Options.AcrServiceConnection);

            WriteNonEmptySummaryForImageDigests(_skippedAnnotationImageDigests,
                "The following image digests were skipped because they have existing annotations with matching EOL dates.");

            WriteNonEmptySummaryForImageDigests(_existingAnnotationImageDigests,
                "The following image digests were skipped because they have existing annotations with non-matching EOL dates. These need to be deleted from MAR before they can be re-annotated.");

            WriteNonEmptySummaryForAnnotationDigests(_existingAnnotationDigests,
                "These are the digests of the annotations with the non-matching EOL dates. This JSON can be used as input for the bulk deletion in MAR.");

            WriteNonEmptySummaryForImageDigests(_failedAnnotationImageDigests,
                "The following digests had annotation failures:");

            if (!_existingAnnotationImageDigests.IsEmpty || !_failedAnnotationImageDigests.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Some digest annotations failed or were skipped due to existing non-matching EOL date annotations (failed: {_failedAnnotationImageDigests.Count}, skipped: {_existingAnnotationImageDigests.Count}).");
            }

            File.WriteAllLines(Options.AnnotationDigestsOutputPath, _createdAnnotationDigests.Order());
        }

        private void WriteNonEmptySummaryForAnnotationDigests(IEnumerable<string> annotationDigests, string message)
        {
            if (annotationDigests.Any())
            {
                WriteNonEmptySummary(new BulkDeletionDescription { Digests = [.. annotationDigests] }, message);
            }
        }

        private void WriteNonEmptySummaryForImageDigests(IEnumerable<EolDigestData> eolDigests, string message)
        {
            if (eolDigests.Any())
            {
                WriteNonEmptySummary(new EolAnnotationsData(eolDigests: [.. eolDigests]), message);
            }
        }

        private void WriteNonEmptySummary(object value, string message)
        {
            _loggerService.WriteMessage(message);
            _loggerService.WriteMessage();
            _loggerService.WriteMessage(JsonSerializer.Serialize(value, s_jsonSerializerOptions));
            _loggerService.WriteMessage();
        }

        private void AnnotateDigest(EolDigestData digestData, DateOnly? globalEolDate)
        {
            if (Options.IsDryRun)
            {
                _loggerService.WriteMessage($"[DRY RUN] Set EOL annotation for digest '{digestData.Digest}'");
                return;
            }

            DateOnly? eolDate = digestData.EolDate ?? globalEolDate;
            if (eolDate is null)
            {
                _failedAnnotationImageDigests.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate });
                _loggerService.WriteError($"EOL date is not specified for digest '{digestData.Digest}'.");
                return;
            }

            if (!_lifecycleMetadataService.IsDigestAnnotatedForEol(digestData.Digest, Options.IsDryRun, out Manifest? existingAnnotationManifest))
            {
                _loggerService.WriteMessage($"Annotating EOL for digest '{digestData.Digest}', date '{eolDate}'");
                if (_lifecycleMetadataService.AnnotateEolDigest(digestData.Digest, eolDate.Value, Options.IsDryRun, out Manifest? createdAnnotationManifest))
                {
                    _createdAnnotationDigests.Add(createdAnnotationManifest.Reference);
                }
                else
                {
                    // We will capture all failures and log the json data at the end.
                    // Json data can be used to rerun the failed annotations.
                    _failedAnnotationImageDigests.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate, Tag = digestData.Tag });
                }
            }
            else
            {
                if (existingAnnotationManifest.Annotations[LifecycleMetadataService.EndOfLifeAnnotation] == eolDate?.ToString(LifecycleMetadataService.EolDateFormat))
                {
                    _loggerService.WriteMessage($"Skipping digest '{digestData.Digest}' because it is already annotated with a matching EOL date.");
                    _skippedAnnotationImageDigests.Add(digestData);
                }
                else
                {
                    _loggerService.WriteError($"Could not annotate digest '{digestData.Digest}' because it has an existing non-matching EOL date: {eolDate}.");
                    _existingAnnotationImageDigests.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate });

                    // Reference is a fully-qualified digest name. We want to remove the registry and repo prefix from the name to reflect the repo-qualified
                    // name that exists in MAR.
                    string refDigest = existingAnnotationManifest.Reference.TrimStartString($"{Options.AcrName}/{Options.RepoPrefix}");
                    _existingAnnotationDigests.Add(refDigest);
                }
            }
        }

        private static EolAnnotationsData LoadEolAnnotationsData(string eolDigestsListPath)
        {
            string eolAnnotationsJson = File.ReadAllText(eolDigestsListPath);
            EolAnnotationsData? eolAnnotations = JsonSerializer.Deserialize<EolAnnotationsData>(eolAnnotationsJson);
            return eolAnnotations is null
                ? throw new JsonException($"Unable to correctly deserialize path '{eolAnnotationsJson}'.")
                : eolAnnotations;
        }
    }
}
