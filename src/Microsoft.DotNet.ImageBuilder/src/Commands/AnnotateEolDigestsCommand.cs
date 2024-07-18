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
            EolAnnotationsData eolAnnotations = LoadEolAnnotationsData(Options.EolDigestsListPath);
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

            if (!_failedAnnotations.IsEmpty)
            {
                _loggerService.WriteMessage("JSON file for rerunning failed annotations:");
                _loggerService.WriteMessage("");
                _loggerService.WriteMessage(JsonConvert.SerializeObject(new EolAnnotationsData(eolDigests: [.. _failedAnnotations])));
                _loggerService.WriteMessage("");
                throw new InvalidOperationException($"Failed to annotate {_failedAnnotations.Count} digests for EOL.");
            }
        }

        private void AnnotateDigest(EolDigestData digestData, DateOnly? globalEolDate)
        {
            if (Options.Force || !_orasService.IsDigestAnnotatedForEol(digestData.Digest, _loggerService, Options.IsDryRun))
            {
                DateOnly? eolDate = digestData.EolDate ?? globalEolDate;
                if (eolDate != null)
                {
                    _loggerService.WriteMessage($"Annotating EOL for digest '{digestData.Digest}', date '{eolDate}'");
                    if (!_orasService.AnnotateEolDigest(digestData.Digest, eolDate.Value, _loggerService, Options.IsDryRun))
                    {
                        // We will capture all failures and log the json data at the end.
                        // Json data can be used to rerun the failed annotations.
                        _failedAnnotations.Add(new EolDigestData { Digest = digestData.Digest, EolDate = eolDate });
                    }
                }
                else
                {
                    _loggerService.WriteError($"EOL date is not specified for digest '{digestData.Digest}'.");
                }
            }
            else
            {
                _loggerService.WriteMessage($"Digest '{digestData.Digest}' is already annotated for EOL.");
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
