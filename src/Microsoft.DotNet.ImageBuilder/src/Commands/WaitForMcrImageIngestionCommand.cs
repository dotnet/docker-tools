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
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class WaitForMcrImageIngestionCommand : ManifestCommand<WaitForMcrImageIngestionOptions, WaitForMcrImageIngestionOptionsBuilder>
    {
        private readonly ILoggerService _loggerService;
        private readonly IMcrStatusClientFactory _mcrStatusClientFactory;
        private readonly IEnvironmentService _environmentService;

        [ImportingConstructor]
        public WaitForMcrImageIngestionCommand(
            ILoggerService loggerService, IMcrStatusClientFactory mcrStatusClientFactory, IEnvironmentService environmentService)
        {
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _mcrStatusClientFactory = mcrStatusClientFactory ?? throw new ArgumentNullException(nameof(mcrStatusClientFactory));
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        protected override string Description => "Waits for images to complete ingestion into MCR";

        protected override async Task ExecuteCoreAsync()
        {
            _loggerService.WriteHeading("WAITING FOR IMAGE INGESTION");

            if (!Options.IsDryRun)
            {
                IMcrStatusClient statusClient = _mcrStatusClientFactory.Create(
                    Options.ServicePrincipal.Tenant,
                    Options.ServicePrincipal.ClientId,
                    Options.ServicePrincipal.Secret);

                IEnumerable<ImageResultInfo> imageResultInfos = await WaitForImageIngestionAsync(statusClient);

                _loggerService.WriteMessage();

                await LogResults(statusClient, imageResultInfos);
            }

            _loggerService.WriteMessage("Image ingestion complete!");
        }

        private IEnumerable<DigestInfo> GetImageDigestInfos(ImageArtifactDetails imageArtifactDetails) =>
            imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images.SelectMany(image => GetImageDigestInfos(image, repo)));

        private IEnumerable<DigestInfo> GetImageDigestInfos(ImageData image, RepoData repo)
        {
            if (image.Manifest?.Digest != null)
            {
                string digestSha = DockerHelper.GetDigestSha(image.Manifest.Digest);
                yield return new DigestInfo(digestSha, repo.Repo, image.Manifest.SharedTags);

                // Find all syndicated shared tags grouped by their syndicated repo
                IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = image.ManifestImage.SharedTags
                    .Where(tag => image.Manifest.SharedTags.Contains(tag.Name) && tag.SyndicatedRepo != null)
                    .GroupBy(tag => tag.SyndicatedRepo);

                foreach (IGrouping<string, TagInfo> syndicatedTags in syndicatedTagGroups)
                {
                    string syndicatedRepo = Options.RepoPrefix + syndicatedTags.Key;

                    string tag = syndicatedTags.First().SyndicatedDestinationTags.First();
                    tag = DockerHelper.GetImageName(Manifest.Registry, syndicatedRepo, tag);

                    yield return new DigestInfo(
                        digestSha,
                        syndicatedRepo,
                        syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags));
                }
            }

            foreach (PlatformData platform in image.Platforms)
            {
                string sha = DockerHelper.GetDigestSha(platform.Digest);

                yield return new DigestInfo(sha, repo.Repo, platform.SimpleTags);

                // Find all syndicated simple tags grouped by their syndicated repo
                IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = platform.PlatformInfo.Tags
                    .Where(tagInfo => platform.SimpleTags.Contains(tagInfo.Name) && tagInfo.SyndicatedRepo != null)
                    .GroupBy(tagInfo => tagInfo.SyndicatedRepo);

                foreach (IGrouping<string, TagInfo> syndicatedTags in syndicatedTagGroups)
                {
                    string syndicatedRepo = syndicatedTags.Key;
                    yield return new DigestInfo(
                        sha,
                        Options.RepoPrefix + syndicatedRepo,
                        syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags));
                }
            }
        }

        private async Task LogResults(IMcrStatusClient statusClient, IEnumerable<ImageResultInfo> imageResultInfos)
        {
            _loggerService.WriteHeading("IMAGE RESULTS");

            List<Task<string>> failedStatusTasks = new List<Task<string>>();

            IEnumerable<ImageResultInfo> failedResults = imageResultInfos
                // Find any result where all of the statuses of a given tag have failed
                .Where(result => result.ImageResult.Value
                    .Where(status => ShouldProcessImageStatus(status, result.DigestInfo))
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
                _loggerService.WriteMessage();
                _loggerService.WriteMessage("Querying details of failed results...");
                _loggerService.WriteMessage();

                await Task.WhenAll(failedStatusTasks);
            }

            if (successfulResults.Any())
            {
                _loggerService.WriteSubheading("Successful results");
                foreach (ImageResultInfo imageResult in successfulResults)
                {
                    _loggerService.WriteMessage(GetQualifiedDigest(imageResult.DigestInfo.Repo, imageResult.DigestInfo.Digest));
                    string tags = string.Join(", ",
                        imageResult.ImageResult.Value
                            .Where(imageStatus => imageStatus.OverallStatus == StageStatus.Succeeded)
                            .Select(imageStatus => imageStatus.Tag));
                    _loggerService.WriteMessage($"\tTags: {tags}");
                    _loggerService.WriteMessage();
                }
            }

            if (failedStatusTasks.Any())
            {
                _loggerService.WriteSubheading("Failed results");

                foreach (Task<string> failedStatusTask in failedStatusTasks)
                {
                    _loggerService.WriteError(failedStatusTask.Result);
                    _loggerService.WriteMessage();
                }

                _environmentService.Exit(1);
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

        private bool ShouldProcessImageStatus(ImageStatus imageStatus, DigestInfo digestInfo) =>
            // Find the image statuses that are associated with the repo indicated in the image info. This filter is needed
            // because MCR's webhook responds to all image pushes in the ACR, even those to staging locations. A queue time filter
            // is needed in order to filter out onboarding requests from a previous ingestion of the same digests.
            imageStatus.TargetRepository == digestInfo.Repo && imageStatus.QueueTime >= Options.MinimumQueueTime;

        private async Task<ImageResultInfo> ReportImageStatusAsync(IMcrStatusClient statusClient, DigestInfo digestInfo)
        {
            string qualifiedDigest = GetQualifiedDigest(digestInfo.Repo, digestInfo.Digest);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Querying image status for '{qualifiedDigest}'");
            stringBuilder.AppendLine("Remaining tags:");
            digestInfo.RemainingTags.ForEach(tag => stringBuilder.AppendLine(tag));
            _loggerService.WriteMessage(stringBuilder.ToString());

            ImageResult imageResult = await statusClient.GetImageResultAsync(digestInfo.Digest);

            IEnumerable<ImageStatus> imageStatuses = imageResult.Value
                .Where(status => ShouldProcessImageStatus(status, digestInfo));

            if (imageStatuses.Any())
            {
                stringBuilder = new StringBuilder();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"Image status results for '{qualifiedDigest}':");

                IEnumerable<IGrouping<string, ImageStatus>> statusesByTag = imageStatuses.GroupBy(status => status.Tag);

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

                _loggerService.WriteMessage(stringBuilder.ToString());
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
            public DigestInfo(string digest, string repo, IEnumerable<string> tags)
            {
                Digest = digest;
                Repo = repo;
                RemainingTags = tags.OrderBy(tag => tag).ToList();
            }

            public string Digest { get; }

            public string Repo { get; }

            /// <summary>
            /// List of tags that need to still be awaited.
            /// </summary>
            public List<string> RemainingTags { get; }
        }
    }
}
