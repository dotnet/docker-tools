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
    [Export]
    public class PublishImageInfoCommand : Command<PublishImageInfoOptions>
    {
        public override async Task ExecuteAsync()
        {
            RepoData[] srcRepos = JsonConvert.DeserializeObject<RepoData[]>(File.ReadAllText(Options.ImageInfoPath));

            await GitHelper.ExecuteGitOperationsWithRetryAsync(Options.GitOptions, async client =>
            {
                GitReference gitRef = await GitHelper.PushChangesAsync(client, Options.GitOptions, "Merging image info updates from build.", async branch =>
                {
                    string originalTargetImageInfoContents = await client.GetGitHubFileContentsAsync(Options.GitOptions.Path, branch);
                    List<RepoData> targetRepos = JsonConvert.DeserializeObject<RepoData[]>(originalTargetImageInfoContents).ToList();

                    ImageInfoHelper.MergeRepos(srcRepos, targetRepos);

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

                if (gitRef != null)
                {
                    Uri commitUrl = GitHelper.GetCommitUrl(Options.GitOptions, gitRef.Object.Sha);
                    Logger.WriteMessage($"The '{Options.ImageInfoPath}' file was updated ({commitUrl}).");
                }
                else
                {
                    Logger.WriteMessage($"No changes to the '{Options.ImageInfoPath}' file were needed.");
                }
            });
        }
    }
}
