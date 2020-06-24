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
    public class WaitForReadmePublishCommand : Command<WaitForReadmePublishOptions>
    {
        private readonly ILoggerService loggerService;
        private readonly IMcrStatusClientFactory mcrStatusClientFactory;

        [ImportingConstructor]
        public WaitForReadmePublishCommand(
            ILoggerService loggerService, IMcrStatusClientFactory mcrStatusClientFactory)
        {
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.mcrStatusClientFactory = mcrStatusClientFactory ?? throw new ArgumentNullException(nameof(mcrStatusClientFactory));
        }

        public override async Task ExecuteAsync()
        {
            IMcrStatusClient statusClient = this.mcrStatusClientFactory.Create(
                Options.ServicePrincipalOptions.Tenant, Options.ServicePrincipalOptions.ClientId, Options.ServicePrincipalOptions.Secret);

            this.loggerService.WriteHeading("QUERYING COMMIT RESULT");

            var result = await WaitForPublishAsync(statusClient);

            LogResults(result.CommitResult, result.CommitStatus);
        }

        private async Task<(CommitResult CommitResult, CommitStatus CommitStatus)> WaitForPublishAsync(IMcrStatusClient statusClient)
        {
            CommitResult commitResult = null;
            CommitStatus commitStatus = null;

            DateTime startTime = DateTime.Now;
            bool isComplete = false;
            while (!isComplete)
            {
                commitResult = await statusClient.GetCommitResultAsync(Options.CommitDigest);

                if (commitResult.Value.Count > 1)
                {
                    this.loggerService.WriteMessage($"WARNING:" + Environment.NewLine +
                        $"Multiple commit statuses were found for commit digest '{Options.CommitDigest}'. " +
                        $"This happens when the same commit digest is submitted for MCR onboarding more than once which is not a workflow that the " +
                        $".NET Docker infrastructure can currently execute. The request that was last queued will be awaited.");
                }

                commitStatus = commitResult.Value
                    .OrderBy(status => status.QueueTime)
                    .Last();

                switch (commitStatus.OverallStatus)
                {
                    case StageStatus.Processing:
                    case StageStatus.NotStarted:
                        await Task.Delay(Options.RequeryDelay);
                        break;
                    case StageStatus.Failed:
                        throw new InvalidOperationException(await GetFailureResultsAsync(statusClient, commitStatus));
                    case StageStatus.Succeeded:
                        isComplete = true;
                        break;
                    case StageStatus.NotApplicable:
                    default:
                        throw new NotSupportedException(
                            $"Unexpected status for commit digest '{Options.CommitDigest}' with request ID '{commitStatus.OnboardingRequestId}: {commitStatus.OverallStatus}");
                }

                if (DateTime.Now - startTime >= Options.WaitTimeout)
                {
                    throw new TimeoutException($"Timed out after '{Options.WaitTimeout}' waiting for the images to be published.");
                }
            }

            return (commitResult, commitStatus);
        }

        private void LogResults(CommitResult commitResult, CommitStatus commitStatus)
        {
            this.loggerService.WriteMessage("Commit info:");
            this.loggerService.WriteMessage($"\tCommit digest: {commitResult.CommitDigest}");
            this.loggerService.WriteMessage($"\tStatus: {commitStatus.OverallStatus}");
            this.loggerService.WriteMessage($"\tBranch: {commitResult.Branch}");
            this.loggerService.WriteMessage("\tFiles updated:");
            commitResult.ContentFiles.ForEach(file => this.loggerService.WriteMessage($"\t\t{file}"));

            this.loggerService.WriteMessage();

            this.loggerService.WriteMessage("Readme publishing complete!");
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
