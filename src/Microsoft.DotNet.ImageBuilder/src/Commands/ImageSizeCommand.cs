﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public delegate void ImageHandler(string repoId, string imageId, string imageTag);

    public abstract class ImageSizeCommand<TOptions> : ManifestCommand<TOptions>
        where TOptions : ImageSizeOptions, new()
    {
        public ImageSizeCommand(IDockerService dockerService)
        {
            this.DockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
        }

        protected IDockerService DockerService { get; }

        protected void ProcessImages(ImageHandler processImage)
        {
            foreach (RepoInfo repo in Manifest.FilteredRepos.Where(platform => platform.FilteredImages.Any()))
            {
                IEnumerable<PlatformInfo> platforms = repo.FilteredImages
                    .SelectMany(image => image.FilteredPlatforms);
                
                foreach (PlatformInfo platform in platforms)
                {
                    string tagName = platform.Tags.First().FullyQualifiedName;
                    processImage(repo.Model.Name, platform.Model.Dockerfile, tagName);
                }
            }
        }

        protected long GetImageSize(string tagName)
        {
            bool localImageExists = DockerService.LocalImageExists(tagName, Options.IsDryRun);
            try
            {
                if (Options.IsPullEnabled)
                {
                    DockerService.PullImage(tagName, Options.IsDryRun);
                }
                else if (!localImageExists)
                {
                    throw new InvalidOperationException($"Image '{tagName}' not found locally");
                }

                return DockerService.GetImageSize(tagName, Options.IsDryRun);
            }
            finally
            {
                // If we had to pull the image because it didn't exist locally, be sure to clean it up
                if (!localImageExists)
                {
                    string imageId = DockerService.GetImageId(tagName, Options.IsDryRun);
                    if (imageId != null)
                    {
                        DockerService.DeleteImage(imageId, Options.IsDryRun);
                    }
                }
            }
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
