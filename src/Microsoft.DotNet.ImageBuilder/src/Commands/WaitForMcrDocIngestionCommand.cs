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
    public class WaitForMcrDocIngestionCommand : Command<WaitForMcrDocIngestionOptions>
    {
        private readonly ILoggerService loggerService;
        private readonly IMcrStatusClientFactory mcrStatusClientFactory;
        private readonly IEnvironmentService environmentService;

        [ImportingConstructor]
        public WaitForMcrDocIngestionCommand(
            ILoggerService loggerService, IMcrStatusClientFactory mcrStatusClientFactory, IEnvironmentService environmentService)
        {
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.mcrStatusClientFactory = mcrStatusClientFactory ?? throw new ArgumentNullException(nameof(mcrStatusClientFactory));
            this.environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        public override async Task ExecuteAsync()
        {
            IMcrStatusClient statusClient = this.mcrStatusClientFactory.Create(
                Options.ServicePrincipal.Tenant, Options.ServicePrincipal.ClientId, Options.ServicePrincipal.Secret);

            this.loggerService.WriteHeading("QUERYING COMMIT RESULT");

            CommitResult result = await WaitForIngestionAsync(statusClient);

            LogSuccessfulResults(result);
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
                    this.loggerService.WriteMessage(
                        $"Readme status results for commit digest '{Options.CommitDigest}' with request ID '{commitStatus.OnboardingRequestId}': {commitStatus.OverallStatus}");

                    switch (commitStatus.OverallStatus)
                    {
                        case StageStatus.Processing:
                        case StageStatus.NotStarted:
                            await Task.Delay(Options.RequeryDelay);
                            break;
                        case StageStatus.Failed:
                            this.loggerService.WriteError(await GetFailureResultsAsync(statusClient, commitStatus));
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
                    this.loggerService.WriteError("Doc ingestion failed.");
                    this.environmentService.Exit(1);
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
            this.loggerService.WriteMessage("Commit info:");
            this.loggerService.WriteMessage($"\tCommit digest: {commitResult.CommitDigest}");
            this.loggerService.WriteMessage($"\tBranch: {commitResult.Branch}");
            this.loggerService.WriteMessage("\tFiles updated:");
            commitResult.ContentFiles.ForEach(file => this.loggerService.WriteMessage($"\t\t{file}"));

            this.loggerService.WriteMessage();

            this.loggerService.WriteMessage("Doc ingestion successfully completed!");
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
