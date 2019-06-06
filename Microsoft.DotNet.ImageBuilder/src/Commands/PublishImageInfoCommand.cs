﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishImageInfoCommand : Command<PublishImageInfoOptions>
    {
        public override async Task ExecuteAsync()
        {
            List<RepoData[]> srcReposList = Directory.EnumerateFiles(Options.SourceImageInfoFolderPath, "*.json", SearchOption.AllDirectories)
                .Select(imageDataPath => JsonConvert.DeserializeObject<RepoData[]>(File.ReadAllText(imageDataPath)))
                .ToList();

            await GitHelper.ExecuteGitOperationsWithRetryAsync(Options.GitOptions, async client =>
            {
                await GitHelper.PushChangesAsync(client, Options.GitOptions, "Merging image info updates from build.", async branch =>
                {
                    string originalTargetImageInfoContents = await client.GetGitHubFileContentsAsync(Options.GitOptions.Path, branch);
                    List<RepoData> targetRepos = JsonConvert.DeserializeObject<RepoData[]>(originalTargetImageInfoContents).ToList();

                    foreach (RepoData[] srcRepos in srcReposList)
                    {
                        MergeRepos(srcRepos, targetRepos);
                    }

                    string newTargetImageInfoContents = JsonHelper.SerializeObject(targetRepos.OrderBy(r => r.Repo).ToArray()) + Environment.NewLine;

                    if (originalTargetImageInfoContents != newTargetImageInfoContents)
                    {
                        GitObject imageInfoGitObject = new GitObject
                        {
                            Path = Options.GitOptions.Path,
                            Type = GitObject.TypeBlob,
                            Mode = GitObject.ModeFile,
                            Content = newTargetImageInfoContents
                        };

                        return new GitObject[] { imageInfoGitObject };
                    }
                    else
                    {
                        return Enumerable.Empty<GitObject>();
                    }
                });
            });
        }

        private void MergeRepos(RepoData[] srcRepos, List<RepoData> targetRepos)
        {
            foreach (RepoData srcRepo in srcRepos)
            {
                RepoData targetRepo = targetRepos.FirstOrDefault(r => r.Repo == srcRepo.Repo);
                if (targetRepo == null)
                {
                    targetRepos.Add(srcRepo);
                }
                else
                {
                    MergeImages(srcRepo, targetRepo);
                }
            }
        }

        private void MergeImages(RepoData srcRepo, RepoData targetRepo)
        {
            if (srcRepo.Images == null)
            {
                return;
            }

            if (srcRepo.Images.Any() && targetRepo.Images == null)
            {
                targetRepo.Images = srcRepo.Images;
                return;
            }

            foreach (KeyValuePair<string, ImageData> srcKvp in srcRepo.Images)
            {
                if (targetRepo.Images.TryGetValue(srcKvp.Key, out ImageData targetImage))
                {
                    MergeDigests(srcKvp.Value, targetImage);
                }
                else
                {
                    targetRepo.Images.Add(srcKvp.Key, srcKvp.Value);
                }
            }
        }

        private void MergeDigests(ImageData srcImage, ImageData targetImage)
        {
            if (srcImage.BaseImages == null)
            {
                return;
            }

            if (srcImage.BaseImages.Any() && targetImage.BaseImages == null)
            {
                targetImage.BaseImages = srcImage.BaseImages;
                return;
            }

            foreach (KeyValuePair<string, string> srcKvp in srcImage.BaseImages)
            {
                targetImage.BaseImages[srcKvp.Key] = srcKvp.Value;
            }
        }
    }
}
