// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class WaitForMcrImageIngestionCommand : ManifestCommand<WaitForMcrImageIngestionOptions>
    {
        private readonly ILoggerService loggerService;
        private readonly IMcrStatusClientFactory mcrStatusClientFactory;
        private readonly IEnvironmentService environmentService;

        [ImportingConstructor]
        public WaitForMcrImageIngestionCommand(
            ILoggerService loggerService, IMcrStatusClientFactory mcrStatusClientFactory, IEnvironmentService environmentService)
        {
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.mcrStatusClientFactory = mcrStatusClientFactory ?? throw new ArgumentNullException(nameof(mcrStatusClientFactory));
            this.environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        public override async Task ExecuteAsync()
        {
            loggerService.WriteHeading("WAITING FOR IMAGE INGESTION");

            if (!Options.IsDryRun)
            {
                IMcrStatusClient statusClient = this.mcrStatusClientFactory.Create(
                    Options.ServicePrincipal.Tenant,
                    Options.ServicePrincipal.ClientId,
                    Options.ServicePrincipal.Secret);

                IEnumerable<ImageResultInfo> imageResultInfos = await this.WaitForImageIngestionAsync(statusClient);

                loggerService.WriteMessage();

                await this.LogResults(statusClient, imageResultInfos);
            }

            loggerService.WriteMessage("Image ingestion complete!");
        }

        private static IEnumerable<DigestInfo> GetImageDigestInfos(ImageArtifactDetails imageArtifactDetails) =>
            imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images.SelectMany(image => GetImageDigestInfos(image, repo)));

        private static IEnumerable<DigestInfo> GetImageDigestInfos(ImageData image, RepoData repo)
        {
            if (image.Manifest?.Digest != null)
            {
                yield return new DigestInfo(DockerHelper.GetDigestSha(image.Manifest.Digest), repo, image.Manifest.SharedTags);
            }

            foreach (PlatformData platform in image.Platforms)
            {
                yield return new DigestInfo(DockerHelper.GetDigestSha(platform.Digest), repo, platform.SimpleTags);
            }
        }

        private async Task LogResults(IMcrStatusClient statusClient, IEnumerable<ImageResultInfo> imageResultInfos)
        {
            this.loggerService.WriteHeading("IMAGE RESULTS");

            List<Task<string>> failedStatusTasks = new List<Task<string>>();

            IEnumerable<ImageResultInfo> failedResults = imageResultInfos
                // Find any result where all of the statuses of a given tag have failed
                .Where(result => result.ImageResult.Value
                    .GroupBy(status => status.Tag)
                    .Any(statusGroup => statusGroup.All(status => status.OverallStatus == StageStatus.Failed)))
                .OrderBy(result => result.DigestInfo.Repo)
                .ThenBy(result => result.DigestInfo.Digest);
            IEnumerable<ImageResultInfo> successfulResults = imageResultInfos.Except(failedResults);

            foreach (ImageResultInfo result in failedResults)
            {
                IEnumerable<ImageStatus> failedStatuses = result.ImageResult.Value.Where(status => status.OverallStatus == StageStatus.Failed);
                List<Task<string>> resultFailedStatusTasks = failedStatuses
                    .Select(status => (
                        result.DigestInfo.Digest,
                        Status: status
                    ))
                    .OrderBy(statusInfo => statusInfo.Status.Tag)
                    .Select(statusInfo => GetFailedStatusAsync(statusClient, result.DigestInfo.Digest, statusInfo.Status))
                    .ToList();

                failedStatusTasks.AddRange(resultFailedStatusTasks);
            }

            if (failedStatusTasks.Any())
            {
                this.loggerService.WriteMessage();
                this.loggerService.WriteMessage("Querying details of failed results...");
                this.loggerService.WriteMessage();

                await Task.WhenAll(failedStatusTasks);
            }

            if (successfulResults.Any())
            {
                this.loggerService.WriteSubheading("Successful results");
                foreach (ImageResultInfo imageResult in successfulResults)
                {
                    this.loggerService.WriteMessage(GetQualifiedDigest(imageResult.DigestInfo.Repo.Repo, imageResult.DigestInfo.Digest));
                    string tags = String.Join(", ",
                        imageResult.ImageResult.Value
                            .Where(imageStatus => imageStatus.OverallStatus == StageStatus.Succeeded)
                            .Select(imageStatus => imageStatus.Tag));
                    this.loggerService.WriteMessage($"\tTags: {tags}");
                    this.loggerService.WriteMessage();
                }
            }

            if (failedStatusTasks.Any())
            {
                this.loggerService.WriteSubheading("Failed results");

                foreach (Task<string> failedStatusTask in failedStatusTasks)
                {
                    this.loggerService.WriteError(failedStatusTask.Result);
                    this.loggerService.WriteMessage();
                }

                this.environmentService.Exit(1);
            }
        }

        private async Task<IEnumerable<ImageResultInfo>> WaitForImageIngestionAsync(IMcrStatusClient statusClient)
        {
            ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            List<Task<ImageResultInfo>> tasks = GetImageDigestInfos(imageArtifactDetails)
                .Select(digestInfo => ReportImageStatusWithContinuationAsync(statusClient, digestInfo))
                .ToList();

            return await TaskHelper.WhenAll(tasks, Options.WaitTimeout);
        }

        private async Task<ImageResultInfo> ReportImageStatusWithContinuationAsync(IMcrStatusClient statusClient, DigestInfo digestInfo)
        {
            return await (await ReportImageStatusAsync(statusClient, digestInfo)
                .ContinueWith(async task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        if (task.Result.DigestInfo.RemainingTags.Any())
                        {
                            await Task.Delay(Options.RequeryDelay);
                            return await ReportImageStatusWithContinuationAsync(statusClient, digestInfo);
                        }
                        else
                        {
                            return task.Result;
                        }
                    }
                    else if (task.IsFaulted)
                    {
                        throw task.Exception;
                    }

                    return null;
                }));
        }

        private async Task<string> GetFailedStatusAsync(IMcrStatusClient statusClient, string digest, ImageStatus imageStatus)
        {
            ImageResultDetailed result = await statusClient.GetImageResultDetailedAsync(digest, imageStatus.OnboardingRequestId);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Failure for '{GetQualifiedDigest(imageStatus.TargetRepository, digest)}':");
            stringBuilder.AppendLine($"\tID: {imageStatus.OnboardingRequestId}");
            stringBuilder.AppendLine($"\tTag: {imageStatus.Tag}");
            stringBuilder.AppendLine($"\tCommit digest: {result.CommitDigest}");
            stringBuilder.Append(result.Substatus.ToString("\t"));
            return stringBuilder.ToString();
        }

        private static string GetQualifiedDigest(string repo, string imageDigest) => $"{repo}@{imageDigest}";

        private async Task<ImageResultInfo> ReportImageStatusAsync(IMcrStatusClient statusClient, DigestInfo digestInfo)
        {
            string qualifiedDigest = GetQualifiedDigest(digestInfo.Repo.Repo, digestInfo.Digest);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Querying image status for '{qualifiedDigest}'");
            stringBuilder.AppendLine("Remaining tags:");
            digestInfo.RemainingTags.ForEach(tag => stringBuilder.AppendLine(tag));
            this.loggerService.WriteMessage(stringBuilder.ToString());

            ImageResult imageResult = await statusClient.GetImageResultAsync(digestInfo.Digest);

            // Find the image statuses that are associated with the repo indicated in the image info. This filter is needed
            // because MCR's webhook responds to all image pushes in the ACR, even those to staging locations. A queue time filter
            // is needed in order to filter out onboarding requests from a previous ingestion of the same digests.
            IEnumerable<ImageStatus> imageStatuses = imageResult.Value
                .Where(status => status.TargetRepository == digestInfo.Repo.Repo && status.QueueTime >= Options.MinimumQueueTime);

            if (imageStatuses.Any())
            {
                stringBuilder = new StringBuilder();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"Image status results for '{qualifiedDigest}':");

                var statusesByTag = imageStatuses.GroupBy(status => status.Tag);

                foreach (IGrouping<string, ImageStatus> tagImageStatuses in statusesByTag)
                {
                    foreach (ImageStatus imageStatus in tagImageStatuses)
                    {
                        stringBuilder.AppendLine(
                            $"Status for tag '{imageStatus.Tag}' with request ID '{imageStatus.OnboardingRequestId}': {imageStatus.OverallStatus}");

                        switch (imageStatus.OverallStatus)
                        {
                            case StageStatus.Processing:
                            case StageStatus.NotStarted:
                            case StageStatus.Failed:
                                break;
                            case StageStatus.Succeeded:
                                // If we've found at least one successful overall status for the tag, we're done with that tag.
                                digestInfo.RemainingTags.Remove(imageStatus.Tag);
                                break;
                            case StageStatus.NotApplicable:
                            default:
                                throw new NotSupportedException(
                                    $"Unexpected image status for digest '{qualifiedDigest}' with tag '{imageStatus.Tag}' and request ID '{imageStatus.OnboardingRequestId}': {imageStatus.OverallStatus}");
                        }
                    }

                    // If all found statuses for a given tag have failed, we're done with that tag.
                    if (tagImageStatuses.All(status => status.OverallStatus == StageStatus.Failed))
                    {
                        digestInfo.RemainingTags.Remove(tagImageStatuses.Key);
                    }
                }

                this.loggerService.WriteMessage(stringBuilder.ToString());
            }

            return new ImageResultInfo
            {
                ImageResult = imageResult,
                DigestInfo = digestInfo
            };
        }

        private class ImageResultInfo
        {
            public ImageResult ImageResult { get; set; }
            public DigestInfo DigestInfo { get; set; }
        }

        private class DigestInfo
        {
            public DigestInfo(string digest, RepoData repo, IEnumerable<string> tags)
            {
                Digest = digest;
                Repo = repo;
                RemainingTags = tags.OrderBy(tag => tag).ToList();
            }

            public string Digest { get; }

            public RepoData Repo { get; }

            /// <summary>
            /// List of tags that need to still be awaited.
            /// </summary>
            public List<string> RemainingTags { get; }
        }
    }
}
