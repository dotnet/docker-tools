// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class WaitForMcrDocIngestionCommand : Command<WaitForMcrDocIngestionOptions, WaitForMcrDocIngestionOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;
        private readonly IMcrStatusClientFactory _mcrStatusClientFactory;
        private readonly IEnvironmentService _environmentService;

        [ImportingConstructor]
        public WaitForMcrDocIngestionCommand(
            ILoggerService loggerService, IMcrStatusClientFactory mcrStatusClientFactory, IEnvironmentService environmentService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _mcrStatusClientFactory = mcrStatusClientFactory ?? throw new ArgumentNullException(nameof(mcrStatusClientFactory));
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        protected override string Description => "Waits for docs to complete ingestion into Docker Hub";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("QUERYING COMMIT RESULT");

            if (!Options.IsDryRun)
            {
                IMcrStatusClient statusClient = _mcrStatusClientFactory.Create(
                Options.ServicePrincipal.Tenant, Options.ServicePrincipal.ClientId, Options.ServicePrincipal.Secret);

                CommitResult result = await WaitForIngestionAsync(statusClient);

                LogSuccessfulResults(result);
            }

            _loggerService.WriteMessage();

            _loggerService.WriteMessage("Doc ingestion successfully completed!");
        }

        private async Task<CommitResult> WaitForIngestionAsync(IMcrStatusClient statusClient)
        {
            CommitResult commitResult = null;

            DateTime startTime = DateTime.Now;
            bool isComplete = false;
            while (!isComplete)
            {
                commitResult = await statusClient.GetCommitResultAsync(Options.CommitDigest);

                foreach (CommitStatus commitStatus in commitResult.Value)
                {
                    _loggerService.WriteMessage(
                        $"Readme status results for commit digest '{Options.CommitDigest}' with request ID '{commitStatus.OnboardingRequestId}': {commitStatus.OverallStatus}");

                    switch (commitStatus.OverallStatus)
                    {
                        case StageStatus.Processing:
                        case StageStatus.NotStarted:
                            await Task.Delay(Options.RequeryDelay);
                            break;
                        case StageStatus.Failed:
                            _loggerService.WriteError(await GetFailureResultsAsync(statusClient, commitStatus));
                            break;
                        case StageStatus.Succeeded:
                            isComplete = true;
                            break;
                        case StageStatus.NotApplicable:
                        default:
                            throw new NotSupportedException(
                                $"Unexpected status for commit digest '{Options.CommitDigest}' with request ID '{commitStatus.OnboardingRequestId}: {commitStatus.OverallStatus}");
                    }
                }

                if (commitResult.Value.All(status => status.OverallStatus == StageStatus.Failed))
                {
                    _loggerService.WriteError("Doc ingestion failed.");
                    _environmentService.Exit(1);
                }

                if (DateTime.Now - startTime >= Options.WaitTimeout)
                {
                    throw new TimeoutException($"Timed out after '{Options.WaitTimeout}' waiting for the docs to be ingested.");
                }
            }

            return commitResult;
        }

        private void LogSuccessfulResults(CommitResult commitResult)
        {
            _loggerService.WriteMessage("Commit info:");
            _loggerService.WriteMessage($"\tCommit digest: {commitResult.CommitDigest}");
            _loggerService.WriteMessage($"\tBranch: {commitResult.Branch}");
            _loggerService.WriteMessage("\tFiles updated:");
            commitResult.ContentFiles.ForEach(file => _loggerService.WriteMessage($"\t\t{file}"));
        }

        private async Task<string> GetFailureResultsAsync(IMcrStatusClient statusClient, CommitStatus commitStatus)
        {
            CommitResultDetailed result = await statusClient.GetCommitResultDetailedAsync(Options.CommitDigest, commitStatus.OnboardingRequestId);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Failure for commit digest '{Options.CommitDigest}':");
            stringBuilder.AppendLine($"\tID: {result.OnboardingRequestId}");
            stringBuilder.AppendLine($"\tBranch: {result.Branch}");
            result.ContentFiles.ForEach(file => stringBuilder.AppendLine($"\t\t{file}"));
            stringBuilder.Append(result.Substatus.ToString("\t"));
            return stringBuilder.ToString();
        }
    }
}
