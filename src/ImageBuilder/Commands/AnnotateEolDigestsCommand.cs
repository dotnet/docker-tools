// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.MarBulkDeletion;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class AnnotateEolDigestsCommand(
        ILogger<AnnotateEolDigestsCommand> logger,
        ILifecycleMetadataService lifecycleMetadataService,
        IRegistryCredentialsProvider registryCredentialsProvider,
        IArtifactService artifactService)
            : Command<AnnotateEolDigestsOptions>
    {
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

        protected override string Description => "Annotates EOL digests in Docker Registry";

        public override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            string eolDigestsListPath = artifactService.ResolvePath(Options.EolDigestsListPath);
            EolAnnotationsData eolAnnotations = LoadEolAnnotationsData(eolDigestsListPath);
            DateOnly? globalEolDate = eolAnnotations.EolDate;

            await registryCredentialsProvider.ExecuteWithCredentialsAsync(
                Options.IsDryRun,
                async ct =>
                {
                    await Parallel.ForEachAsync(eolAnnotations.EolDigests, ct,
                        async (digestData, ct) => await AnnotateDigestAsync(digestData, globalEolDate, ct));
                },
                Options.CredentialsOptions,
                registryName: Options.AcrName,
                cancellationToken);

            if (!_skippedAnnotationImageDigests.IsEmpty)
            {
                string skippedJson = JsonSerializer.Serialize(
                    new EolAnnotationsData(eolDigests: [.. _skippedAnnotationImageDigests]), s_jsonSerializerOptions);

                logger.LogInformation(
                    "The following image digests were skipped because they have existing annotations"
                        + " with matching EOL dates:\n{ImageDigests}",
                    skippedJson);
            }

            if (!_existingAnnotationImageDigests.IsEmpty)
            {
                string existingJson = JsonSerializer.Serialize(
                    new EolAnnotationsData(eolDigests: [.. _existingAnnotationImageDigests]), s_jsonSerializerOptions);

                logger.LogInformation(
                    "The following image digests were skipped because they have existing annotations"
                        + " with non-matching EOL dates. These need to be deleted from MAR before they can be"
                        + " re-annotated:\n{ImageDigests}",
                    existingJson);
            }

            if (!_existingAnnotationDigests.IsEmpty)
            {
                string bulkDeletionJson = JsonSerializer.Serialize(
                    new BulkDeletionDescription { Digests = [.. _existingAnnotationDigests] }, s_jsonSerializerOptions);

                logger.LogInformation(
                    "These are the digests of the annotations with the non-matching EOL dates."
                        + " This JSON can be used as input for the bulk deletion in MAR:\n{AnnotationDigests}",
                    bulkDeletionJson);
            }

            if (!_failedAnnotationImageDigests.IsEmpty)
            {
                string failedJson = JsonSerializer.Serialize(
                    new EolAnnotationsData(eolDigests: [.. _failedAnnotationImageDigests]), s_jsonSerializerOptions);

                logger.LogError("The following digests had annotation failures:\n{ImageDigests}", failedJson);
            }

            if (!_existingAnnotationImageDigests.IsEmpty || !_failedAnnotationImageDigests.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Some digest annotations failed or were skipped due to existing non-matching EOL date annotations (failed: {_failedAnnotationImageDigests.Count}, skipped: {_existingAnnotationImageDigests.Count}).");
            }

            string annotationDigests = string.Join(Environment.NewLine, _createdAnnotationDigests.Order());
            if (annotationDigests.Length > 0)
            {
                annotationDigests += Environment.NewLine;
            }

            artifactService.WriteAllText(Options.AnnotationDigestsOutputPath, annotationDigests);
        }

        private async Task AnnotateDigestAsync(EolDigestData digestData, DateOnly? globalEolDate, CancellationToken cancellationToken)
        {
            if (Options.IsDryRun)
            {
                logger.LogInformation("[DRY RUN] Set EOL annotation for digest '{Digest}'", digestData.Digest);
                return;
            }

            DateOnly? eolDate = digestData.EolDate ?? globalEolDate;
            if (eolDate is null)
            {
                _failedAnnotationImageDigests.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate });
                logger.LogError("EOL date is not specified for digest '{Digest}'.", digestData.Digest);
                return;
            }

            LifecycleArtifact? existingArtifact = await lifecycleMetadataService
                .GetLatestLifecycleArtifactAsync(digestData.Digest, includeInternal: false, cancellationToken);

            if (existingArtifact is null)
            {
                logger.LogInformation(
                    "Annotating EOL for digest '{Digest}', date '{EolDate}'",
                    digestData.Digest,
                    eolDate);

                LifecycleArtifact? createdArtifact = await lifecycleMetadataService
                    .AnnotateEolDigestAsync(digestData.Digest, eolDate.Value, isInternal: false, cancellationToken);

                if (createdArtifact is not null)
                {
                    _createdAnnotationDigests.Add(createdArtifact.Referrer.Digest);
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
                if (existingArtifact.EndOfLifeDate == eolDate)
                {
                    logger.LogInformation(
                        "Skipping digest '{Digest}' because it is already annotated with a matching EOL date.",
                        digestData.Digest);

                    _skippedAnnotationImageDigests.Add(digestData);
                }
                else
                {
                    logger.LogError(
                        "Could not annotate digest '{Digest}' because its existing EOL date '{ExistingEolDate}'"
                            + " does not match '{EolDate}'.",
                        digestData.Digest,
                        existingArtifact.EndOfLifeDate,
                        eolDate);

                    _existingAnnotationImageDigests.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate });

                    // Reference is a fully-qualified digest name. We want to remove the registry and repo prefix from
                    // the name to reflect the repo-qualified name that exists in MAR.
                    string refDigest = existingArtifact.Referrer.Digest
                        .TrimStartString($"{Options.AcrName}/{Options.RepoPrefix}");

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
