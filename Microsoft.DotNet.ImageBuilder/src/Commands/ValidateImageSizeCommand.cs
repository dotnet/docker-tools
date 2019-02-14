// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ValidateImageSizeCommand : Command<ValidateImageSizeOptions>
    {
        public ValidateImageSizeCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            if (Options.UpdateBaseline)
            {
                UpdateBaseline();
            }
            else
            {
                ValidateImages();
            }

            return Task.CompletedTask;
        }

        private void DisplayResults(IEnumerable<ImageInfo> imageData)
        {
            IEnumerable<ImageInfo> baselinedImageData = imageData.Where(info => info.BaselineSize.HasValue).ToArray();

            Logger.WriteHeading("VALIDATION RESULTS");
            LogResults(
                baselinedImageData.Where(info => info.SizeDifference == 0),
                Logger.WriteMessage,
                "Images with no size change:");
            LogResults(
                baselinedImageData.Where(info =>
                    info.SizeDifference != 0 && info.AllowedVariance > Math.Abs(info.SizeDifference.Value)),
                Logger.WriteMessage,
                "Images with allowed size change:");
            LogResults(
                baselinedImageData.Where(info => info.AllowedVariance < Math.Abs(info.SizeDifference.Value)),
                Logger.WriteError,
                "Images exceeding size variance:");
            LogResults(
                imageData.Except(baselinedImageData),
                Logger.WriteError,
                "Images missing from baseline:");
        }

        private void LogResults(IEnumerable<ImageInfo> imageData, Action<string> logAction, string header)
        {
            if (imageData.Any())
            {
                Logger.WriteSubheading(header);

                foreach (ImageInfo info in imageData)
                {
                    string msg = $"{info.Id}{Environment.NewLine}"
                        + $"    Actual:     {info.CurrentSize,15:N0}{Environment.NewLine}";
                    if (info.BaselineSize.HasValue)
                    {
                        double? minVariance = info.BaselineSize - info.AllowedVariance;
                        double? maxVariance = info.BaselineSize + info.AllowedVariance;

                        msg += $"    Expected:   {info.BaselineSize,15:N0}{Environment.NewLine}"
                        + $"    Difference: {info.SizeDifference,15:N0}{Environment.NewLine}"
                        + $"    Variation Allowed: {minVariance:N0} - {maxVariance:N0}";
                    }

                    logAction(msg);
                }

                Logger.WriteMessage("----------------------------------------------------");
            }
        }

        private void ProcessImages(Func<string, JObject> getRepoJson, Action<string, long, JObject> processImage)
        {
            foreach (RepoInfo repo in Manifest.FilteredRepos.Where(platform => platform.FilteredImages.Any()))
            {
                JObject repoJson = getRepoJson(repo.Model.Name);

                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms);
                foreach (PlatformInfo platform in platforms)
                {
                    string tagName = platform.Tags.First().FullyQualifiedName;

                    if (Options.IsPullEnabled)
                    {
                        DockerHelper.PullImage(tagName, Options.IsDryRun);
                    }
                    else if (!DockerHelper.LocalImageExists(tagName, Options.IsDryRun))
                    {
                        throw new InvalidOperationException($"Image '{tagName}'not found locally");
                    }

                    long imageSize = DockerHelper.GetImageSize(tagName, Options.IsDryRun);
                    processImage(platform.Model.Dockerfile, imageSize, repoJson);
                }
            }
        }

        private void UpdateBaseline()
        {
            Logger.WriteHeading("UPDATING IMAGE SIZE BASELINE");

            JObject json = new JObject();

            JObject getRepoJson(string repoName)
            {
                JObject repoJson = new JObject();
                json[repoName] = repoJson;
                return repoJson;
            }
            void processImage(string imageId, long imageSize, JObject repoJson) =>
                repoJson.Add(imageId, new JValue(imageSize));
            ProcessImages(getRepoJson, processImage);

            Logger.WriteSubheading($"Updating `{Options.BaselinePath}`");
            string formattedJson = json.ToString();
            Logger.WriteMessage(formattedJson);
            File.WriteAllText(Options.BaselinePath, formattedJson);
        }

        private void ValidateImages()
        {
            Logger.WriteHeading("VALIDATING IMAGE SIZES");

            List<ImageInfo> imageData = new List<ImageInfo>();
            string jsonContent = File.ReadAllText(Options.BaselinePath);
            JObject json = JObject.Parse(jsonContent);

            JObject getRepoJson(string repoName) => (JObject)json[repoName];
            void processImage(string imageId, long imageSize, JObject repoJson)
            {
                long? baseline = null;

                if (repoJson.TryGetValue(imageId, out JToken sizeJson))
                {
                    baseline = (long)sizeJson;
                }

                imageData.Add(new ImageInfo()
                {
                    Id = imageId,
                    CurrentSize = imageSize,
                    BaselineSize = baseline,
                    AllowedVariance = baseline * (double)Options.AllowedVariance / 100,
                    SizeDifference = imageSize - baseline
                });
            }
            ProcessImages(getRepoJson, processImage);

            DisplayResults(imageData);
        }

        private class ImageInfo
        {
            public double? AllowedVariance { get; set; }
            public long? BaselineSize { get; set; }
            public long CurrentSize { get; set; }
            public string Id { get; set; }
            public long? SizeDifference { get; set; }
        }
    }
}
