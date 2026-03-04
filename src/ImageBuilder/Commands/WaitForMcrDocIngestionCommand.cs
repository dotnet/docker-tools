#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class WaitForMcrDocIngestionCommand : Command<WaitForMcrDocIngestionOptions, WaitForMcrDocIngestionOptionsBuilder>
    {
        private readonly ILogger<WaitForMcrDocIngestionCommand> _logger;
        private readonly IEnvironmentService _environmentService;
        private readonly Lazy<IMcrStatusClient> _mcrStatusClient;

        public WaitForMcrDocIngestionCommand(
            ILogger<WaitForMcrDocIngestionCommand> logger,
            IMcrStatusClientFactory mcrStatusClientFactory,
            IEnvironmentService environmentService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mcrStatusClient = new Lazy<IMcrStatusClient>(() => mcrStatusClientFactory.Create(Options.MarServiceConnection));
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        protected override string Description => "Waits for docs to complete ingestion into Docker Hub";

        public override async Task ExecuteAsync()
        {
            _logger.LogInformation("QUERYING COMMIT RESULT");

            if (!Options.IsDryRun)
            {
                CommitResult result = await WaitForIngestionAsync(_mcrStatusClient.Value);

                LogSuccessfulResults(result);
            }

            _logger.LogInformation(string.Empty);

            _logger.LogInformation("Doc ingestion successfully completed!");
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
                    _logger.LogInformation(
                        $"Readme status results for commit digest '{Options.CommitDigest}' with request ID '{commitStatus.OnboardingRequestId}': {commitStatus.OverallStatus}");

                    switch (commitStatus.OverallStatus)
                    {
                        case StageStatus.Processing:
                        case StageStatus.NotStarted:
                            await Task.Delay(Options.IngestionOptions.RequeryDelay);
                            break;
                        case StageStatus.Failed:
                            _logger.LogError(await GetFailureResultsAsync(statusClient, commitStatus));
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
                    _logger.LogError("Doc ingestion failed.");
                    _environmentService.Exit(1);
                }

                if (DateTime.Now - startTime >= Options.IngestionOptions.WaitTimeout)
                {
                    throw new TimeoutException($"Timed out after '{Options.IngestionOptions.WaitTimeout}' waiting for the docs to be ingested.");
                }
            }

            return commitResult;
        }

        private void LogSuccessfulResults(CommitResult commitResult)
        {
            _logger.LogInformation("Commit info:");
            _logger.LogInformation($"\tCommit digest: {commitResult.CommitDigest}");
            _logger.LogInformation($"\tBranch: {commitResult.Branch}");
            _logger.LogInformation("\tFiles updated:");
            commitResult.ContentFiles.ForEach(file => _logger.LogInformation($"\t\t{file}"));
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
