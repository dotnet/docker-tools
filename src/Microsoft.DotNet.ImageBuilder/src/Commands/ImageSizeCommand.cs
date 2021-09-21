// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json.Linq;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public delegate void ImageHandler(string repoId, string imageId, string imageTag);

    public abstract class ImageSizeCommand<TOptions, TOptionsBuilder> : ManifestCommand<TOptions, TOptionsBuilder>
        where TOptions : ImageSizeOptions, new()
        where TOptionsBuilder : ImageSizeOptionsBuilder, new()
    {
        public ImageSizeCommand(IDockerService dockerService)
        {
            DockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
        }

        protected IDockerService DockerService { get; }

        protected void ProcessImages(ImageHandler processImage)
        {
            foreach (RepoInfo repo in Manifest.FilteredRepos.Where(platform => platform.FilteredImages.Any()))
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms)
                    .Where(platform => platform.Tags.Any());

                foreach (PlatformInfo platform in platforms)
                {
                    string tagName = platform.Tags.First().FullyQualifiedName;
                    processImage(repo.Name, platform.Model.Dockerfile, tagName);
                }
            }
        }

        protected long GetImageSize(string tagName)
        {
            if (Options.IsPullEnabled)
            {
                DockerService.PullImage(tagName, Options.IsDryRun);
            }
            else if (!DockerService.LocalImageExists(tagName, Options.IsDryRun))
            {
                throw new InvalidOperationException($"Image '{tagName}' not found locally");
            }

            return DockerService.GetImageSize(tagName, Options.IsDryRun);
        }

        protected Dictionary<string, ImageSizeInfo> LoadBaseline()
        {
            if (!File.Exists(Options.BaselinePath))
            {
                throw new FileNotFoundException("No file exists at the specified baseline path.", Options.BaselinePath);
            }

            string jsonContent = File.ReadAllText(Options.BaselinePath);
            JObject json = JObject.Parse(jsonContent);

            return json
                .Children()
                .Cast<JProperty>()
                .SelectMany(repo => repo.Value
                    .Children()
                    .Cast<JProperty>()
                    .Select(image =>
                    {
                        long baseline = (long)image.Value;
                        return new ImageSizeInfo
                        {
                            Id = image.Name,
                            BaselineSize = baseline,
                            AllowedVariance = baseline * (double)Options.AllowedVariance / 100,
                        };
                    }))
                .ToDictionary(imageSizeInfo => imageSizeInfo.Id);
        }
    }
}
#nullable disable
