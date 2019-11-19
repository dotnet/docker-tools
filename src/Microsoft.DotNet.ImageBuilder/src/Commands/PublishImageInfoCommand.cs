// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class PublishImageInfoCommand : Command<PublishImageInfoOptions>
    {
        private readonly IGitHubClientFactory gitHubClientFactory;

        [ImportingConstructor]
        public PublishImageInfoCommand(IGitHubClientFactory gitHubClientFactory)
        {
            this.gitHubClientFactory = gitHubClientFactory ?? throw new ArgumentNullException(nameof(gitHubClientFactory));
        }

        public override async Task ExecuteAsync()
        {
            RepoData[] srcRepos = JsonConvert.DeserializeObject<RepoData[]>(File.ReadAllText(Options.ImageInfoPath));

            using (IGitHubClient gitHubClient = this.gitHubClientFactory.GetClient(Options.GitOptions.ToGitHubAuth()))
            {
                await GitHelper.ExecuteGitOperationsWithRetryAsync(async () =>
                {
                    GitReference gitRef = await GitHelper.PushChangesAsync(gitHubClient, Options.GitOptions, "Merging image info updates from build.", async branch =>
                    {
                        string originalTargetImageInfoContents = await gitHubClient.GetGitHubFileContentsAsync(Options.GitOptions.Path, branch);
                        List<RepoData> targetRepos = JsonConvert.DeserializeObject<RepoData[]>(originalTargetImageInfoContents).ToList();

                        ImageInfoMergeOptions options = new ImageInfoMergeOptions
                        {
                            ReplaceTags = true
                        };

                        ImageInfoHelper.MergeRepos(srcRepos, targetRepos, options);

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

                    Uri imageInfoPathIdentifier = GitHelper.GetBlobUrl(Options.GitOptions);

                    if (gitRef != null)
                    {
                        Uri commitUrl = GitHelper.GetCommitUrl(Options.GitOptions, gitRef.Object.Sha);
                        Logger.WriteMessage($"The '{imageInfoPathIdentifier}' file was updated ({commitUrl}).");
                    }
                    else
                    {
                        Logger.WriteMessage($"No changes to the '{imageInfoPathIdentifier}' file were needed.");
                    }
                });
            }
        }
    }
}
