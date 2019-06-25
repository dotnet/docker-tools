// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class MergeImageInfoFilesCommand : Command<MergeImageInfoFilesOptions>
    {
        public override Task ExecuteAsync()
        {
            List<RepoData[]> srcReposList = Directory.EnumerateFiles(Options.SourceImageInfoFolderPath, "*.json", SearchOption.AllDirectories)
                .Select(imageDataPath => JsonConvert.DeserializeObject<RepoData[]>(File.ReadAllText(imageDataPath)))
                .ToList();

            if (!srcReposList.Any())
            {
                throw new InvalidOperationException(
                    $"No JSON files found in source folder '{Options.SourceImageInfoFolderPath}'");
            }

            List<RepoData> combinedRepos = new List<RepoData>();
            foreach (RepoData[] repos in srcReposList)
            {
                ImageInfoHelper.MergeRepos(repos, combinedRepos);
            }

            string destinationContents = JsonHelper.SerializeObject(combinedRepos.OrderBy(r => r.Repo).ToArray()) + Environment.NewLine;
            File.WriteAllText(Options.DestinationImageInfoPath, destinationContents);

            return Task.CompletedTask;
        }
    }
}
