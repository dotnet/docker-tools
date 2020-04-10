// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishImageInfoCommand : ManifestCommand<PublishImageInfoOptions>
    {
        private readonly IGitHubClientFactory gitHubClientFactory;

        [ImportingConstructor]
        public PublishImageInfoCommand(IGitHubClientFactory gitHubClientFactory)
        {
            this.gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
        }

        public override async Task ExecuteAsync()
        {
            ImageArtifactDetails srcImageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            using (IGitHubClient gitHubClient = this.gitHubClientFactory.GetClient(Options.GitOptions.ToGitHubAuth(), Options.IsDryRun))
            {
                await GitHelper.ExecuteGitOperationsWithRetryAsync(async () =>
                {
                    bool hasChanges = false;
                    GitReference gitRef = await GitHelper.PushChangesAsync(gitHubClient, Options, "Merging image info updates from build.", async branch =>
                    {
                        string originalTargetImageInfoContents = await gitHubClient.GetGitHubFileContentsAsync(Options.GitOptions.Path, branch);
                        ImageArtifactDetails newImageArtifactDetails;

                        if (originalTargetImageInfoContents != null)
                        {
                            ImageArtifactDetails targetImageArtifactDetails = ImageInfoHelper.LoadFromContent(
                                originalTargetImageInfoContents, Manifest, skipManifestValidation: true);

                            RemoveOutOfDateContent(targetImageArtifactDetails);

                            ImageInfoMergeOptions options = new ImageInfoMergeOptions
                            {
                                ReplaceTags = true
                            };

                            ImageInfoHelper.MergeImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails, options);

                            newImageArtifactDetails = targetImageArtifactDetails;
                        }
                        else
                        {
                            // If there is no existing file to update, there's nothing to merge with so the source data
                            // becomes the target data.
                            newImageArtifactDetails = srcImageArtifactDetails;
                        }

                        string newTargetImageInfoContents =
                            JsonHelper.SerializeObject(newImageArtifactDetails) + Environment.NewLine;

                        if (originalTargetImageInfoContents != newTargetImageInfoContents)
                        {
                            GitObject imageInfoGitObject = new GitObject
                            {
                                Path = Options.GitOptions.Path,
                                Type = GitObject.TypeBlob,
                                Mode = GitObject.ModeFile,
                                Content = newTargetImageInfoContents
                            };

                            hasChanges = true;
                            return new GitObject[] { imageInfoGitObject };
                        }
                        else
                        {
                            return Enumerable.Empty<GitObject>();
                        }
                    });

                    Uri imageInfoPathIdentifier = GitHelper.GetBlobUrl(Options.GitOptions);

                    if (hasChanges)
                    {
                        if (!Options.IsDryRun)
                        {
                            Uri commitUrl = GitHelper.GetCommitUrl(Options.GitOptions, gitRef.Object.Sha);
                            Logger.WriteMessage($"The '{imageInfoPathIdentifier}' file was updated ({commitUrl}).");
                        }
                        else
                        {
                            Logger.WriteMessage($"The '{imageInfoPathIdentifier}' file would have been updated.");
                        }
                    }
                    else
                    {
                        Logger.WriteMessage($"No changes to the '{imageInfoPathIdentifier}' file were needed.");
                    }
                });
            }
        }

        private void RemoveOutOfDateContent(ImageArtifactDetails imageArtifactDetails)
        {
            for (int repoIndex = imageArtifactDetails.Repos.Count - 1; repoIndex >= 0; repoIndex--)
            {
                RepoData repoData = imageArtifactDetails.Repos[repoIndex];
                
                // Since the registry name is not represented in the image info, make sure to compare the repo name with the
                // manifest's repo model name which isn't registry-qualified.
                RepoInfo manifestRepo = Manifest.AllRepos.FirstOrDefault(manifestRepo => manifestRepo.Model.Name == repoData.Repo);

                // If there doesn't exist a matching repo in the manifest, remove it from the image info
                if (manifestRepo is null)
                {
                    imageArtifactDetails.Repos.Remove(repoData);
                    continue;
                }

                for (int imageIndex = repoData.Images.Count - 1; imageIndex >= 0; imageIndex--)
                {
                    ImageData imageData = repoData.Images[imageIndex];
                    ImageInfo manifestImage = imageData.ManifestImage;

                    // If there doesn't exist a matching image in the manifest, remove it from the image info
                    if (manifestImage is null)
                    {
                        repoData.Images.Remove(imageData);
                        continue;
                    }
                    
                    for (int platformIndex = imageData.Platforms.Count - 1; platformIndex >= 0; platformIndex--)
                    {
                        PlatformData platformData = imageData.Platforms[platformIndex];
                        PlatformInfo manifestPlatform = manifestImage.AllPlatforms
                            .FirstOrDefault(manifestPlatform => platformData.Equals(manifestPlatform));

                        // If there doesn't exist a matching platform in the manifest, remove it from the image info
                        if (manifestPlatform is null)
                        {
                            imageData.Platforms.Remove(platformData);
                        }
                    }
                }
            }

            if (imageArtifactDetails.Repos.Count == 0)
            {
                // Failsafe to prevent wiping out the image info due to a bug in the logic
                throw new InvalidOperationException(
                    "Removal of out-of-date content resulted in there being no content remaining in the target image info file. Something is probably wrong with the logic.");
            }
        }
    }
}
