// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        private void LogErrors(List<string> messages, string header)
        {
            if (messages.Any())
            {
                Logger.WriteSubheading(header);
                messages.ForEach(msg => Logger.WriteError(msg));
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
            
            Func<string, JObject> getRepoJson = (repoName) =>
            {
                JObject repoJson = new JObject();
                json[repoName] = repoJson;
                return repoJson;
            };
            Action<string, long, JObject> processImage = (imageId, imageSize, repoJson) =>
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

            double allowedVariance = Options.AllowedVariance / 100;
            List<string> missingImages = new List<string>();
            List<string> invalidImages = new List<string>();
            string jsonContent = File.ReadAllText(Options.BaselinePath);
            JObject json = JObject.Parse(jsonContent);

            Func<string, JObject> getRepoJson = (repoName) => (JObject)json[repoName];
            Action<string, long, JObject> processImage = (imageId, imageSize, repoJson) => 
            {
                if (!repoJson.TryGetValue(imageId, out JToken sizeJson))
                {
                    missingImages.Add(imageId);
                }
                else
                {
                    long baseline = (long)sizeJson;
                    double allowedMin = baseline * (1 - allowedVariance);
                    double allowedMax = baseline * (1 + allowedVariance);
                    string msg = $"{imageId} => "
                        + $"Expected: {baseline} Variation Allowed: {allowedMin} - {allowedMax} Actual: {imageSize}";
                    Logger.WriteMessage(msg);
                    if (imageSize < allowedMin || imageSize > allowedMax)
                    {
                        invalidImages.Add(msg);
                    }
                }
            };
            ProcessImages(getRepoJson, processImage);

            Logger.WriteSubheading($"Validation Results:");
            if (missingImages.Any() || invalidImages.Any())
            {
                LogErrors(missingImages, "Images missing from baseline:");
                LogErrors(invalidImages, "Images failing size validation:");
                Environment.Exit(1);
            }
            else
            {
                Logger.WriteMessage("SUCCESS");
            }
        }
    }
}
