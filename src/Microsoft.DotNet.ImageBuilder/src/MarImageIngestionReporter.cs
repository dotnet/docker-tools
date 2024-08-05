// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.McrStatus;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable

public interface IMarImageIngestionReporter
{
   Task ReportImageStatusesAsync(IEnumerable<DigestInfo> digestInfos, TimeSpan timeout, TimeSpan requeryDelay, DateTime? minimumQueueTime);
}

[Export(typeof(IMarImageIngestionReporter))]
public class MarImageIngestionReporter : IMarImageIngestionReporter
{
    private readonly ILoggerService _loggerService;
    private readonly IMcrStatusClient _statusClient;
    private readonly IEnvironmentService _environmentService;

    [ImportingConstructor]
    public MarImageIngestionReporter(ILoggerService loggerService, IMcrStatusClient statusClient, IEnvironmentService environmentService)
    {
        _loggerService = loggerService;
        _statusClient = statusClient;
        _environmentService = environmentService;
    }

    public Task ReportImageStatusesAsync(IEnumerable<DigestInfo> digestInfos, TimeSpan timeout, TimeSpan requeryDelay, DateTime? minimumQueueTime) =>
        new ReporterImpl(_loggerService, _statusClient, _environmentService, timeout, requeryDelay, minimumQueueTime)
            .ReportImageStatusesAsync(digestInfos);

    private class ReporterImpl
    {
        private readonly ILoggerService _loggerService;
        private readonly IMcrStatusClient _statusClient;
        private readonly IEnvironmentService _environmentService;
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _requeryDelay;
        private readonly DateTime? _minimumQueueTime;

        public ReporterImpl(ILoggerService loggerService, IMcrStatusClient statusClient, IEnvironmentService environmentService,
            TimeSpan timeout, TimeSpan requeryDelay, DateTime? minimumQueueTime)
        {
            _loggerService = loggerService;
            _statusClient = statusClient;
            _environmentService = environmentService;
            _timeout = timeout;
            _requeryDelay = requeryDelay;
            _minimumQueueTime = minimumQueueTime;
        }

        public async Task ReportImageStatusesAsync(IEnumerable<DigestInfo> digestInfos)
        {
            List<Task<ImageResultInfo>> tasks = digestInfos
                .Select(digestInfo => ReportImageStatusAsync(digestInfo))
                .ToList();
            IEnumerable<ImageResultInfo> imageResultInfos = await TaskHelper.WhenAll(tasks, _timeout);
            _loggerService.WriteMessage();
            await LogResults(imageResultInfos);
            _loggerService.WriteMessage("Image ingestion complete!");
        }

        private async Task<ImageResultInfo> ReportImageStatusAsync(DigestInfo digestInfo)
        {
            return await (await ReportImageStatusCoreAsync(digestInfo)
                .ContinueWith(async task =>
                {
                    if (task.IsCompletedSuccessfully)
                    {
                        if (!task.Result.DigestInfo.IsComplete)
                        {
                            await Task.Delay(_requeryDelay);
                            return await ReportImageStatusAsync(digestInfo);
                        }
                        else
                        {
                            return task.Result;
                        }
                    }
                    else if (task.Exception is not null)
                    {
                        throw task.Exception;
                    }

                    throw new NotSupportedException();
                }));
        }

        private async Task<ImageResultInfo> ReportImageStatusCoreAsync(DigestInfo digestInfo)
        {
            string qualifiedDigest = GetQualifiedDigest(digestInfo.Repo, digestInfo.Digest);

            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine($"Querying image status for '{qualifiedDigest}'");
            stringBuilder.AppendLine("Remaining tags:");
            digestInfo.RemainingTags.ForEach(tag => stringBuilder.AppendLine(tag));
            _loggerService.WriteMessage(stringBuilder.ToString());

            ImageResult imageResult = await _statusClient.GetImageResultAsync(digestInfo.Digest);

            IEnumerable<ImageStatus> imageStatuses = imageResult.Value
                .Where(status => ShouldProcessImageStatus(status, digestInfo));

            if (imageStatuses.Any())
            {
                stringBuilder = new StringBuilder();
                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"Image status results for '{qualifiedDigest}':");

                IEnumerable<IGrouping<string?, ImageStatus>> statusesByTag = imageStatuses.GroupBy(status => status.Tag);

                foreach (IGrouping<string?, ImageStatus> tagImageStatuses in statusesByTag)
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
                                RemoveTagFromDigest(digestInfo, imageStatus.Tag);
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
                        RemoveTagFromDigest(digestInfo, tagImageStatuses.Key);
                    }
                }

                _loggerService.WriteMessage(stringBuilder.ToString());
            }

            return new ImageResultInfo(imageResult, digestInfo);
        }

        private static void RemoveTagFromDigest(DigestInfo digestInfo, string? tag)
        {
            if (tag is not null)
            {
                digestInfo.RemainingTags.Remove(tag);
            }
            
            if (digestInfo.RemainingTags.Count == 0)
            {
                digestInfo.IsComplete = true;
            }
        }

        private static string GetQualifiedDigest(string repo, string imageDigest) => $"{repo}@{imageDigest}";

        private bool ShouldProcessImageStatus(ImageStatus imageStatus, DigestInfo digestInfo) =>
            // Find the image statuses that are associated with the repo indicated in the image info. This filter is needed
            // because MCR's webhook responds to all image pushes in the ACR, even those to staging locations. A queue time filter
            // is needed in order to filter out onboarding requests from a previous ingestion of the same digests.
            imageStatus.SourceRepository == digestInfo.Repo && (_minimumQueueTime is null || imageStatus.QueueTime >= _minimumQueueTime);

        private async Task LogResults(IEnumerable<ImageResultInfo> imageResultInfos)
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
                    .Select(statusInfo => GetFailedStatusAsync(result.DigestInfo.Digest, statusInfo.Status))
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

        private async Task<string> GetFailedStatusAsync(string digest, ImageStatus imageStatus)
        {
            ImageResultDetailed result = await _statusClient.GetImageResultDetailedAsync(digest, imageStatus.OnboardingRequestId);

            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Failure for '{GetQualifiedDigest(imageStatus.TargetRepository, digest)}':");
            stringBuilder.AppendLine($"\tID: {imageStatus.OnboardingRequestId}");
            stringBuilder.AppendLine($"\tTag: {imageStatus.Tag}");
            stringBuilder.AppendLine($"\tCommit digest: {result.CommitDigest}");
            stringBuilder.AppendLine($"\tReason: {imageStatus.FailureReason}");
            stringBuilder.Append(result.Substatus.ToString("\t"));
            return stringBuilder.ToString();
        }
    }

    private record ImageResultInfo
    {
        public ImageResultInfo(ImageResult imageResult, DigestInfo digestInfo)
        {
            ImageResult = imageResult;
            DigestInfo = digestInfo;
        }

        public ImageResult ImageResult { get; set; }
        public DigestInfo DigestInfo { get; set; }
    }
}

public record DigestInfo
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

    /// <summary>
    /// Gets or sets a value indicating whether processing of this digest is complete.
    /// This is not an indication of success.
    /// </summary>
    public bool IsComplete { get; set; }
}
