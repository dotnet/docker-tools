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
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class MergeImageInfoCommand : Command<MergeImageInfoOptions>
    {
        public override Task ExecuteAsync()
        {
            var imageInfoFiles = Directory.EnumerateFiles(
                Options.SourceImageInfoFolderPath,
                "*.json",
                SearchOption.AllDirectories);

            List<RepoData[]> srcReposList = imageInfoFiles
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

            RepoData[] reposArray = combinedRepos
                .OrderBy(r => r.Repo)
                .ToArray();

            string destinationContents = JsonHelper.SerializeObject(reposArray) + Environment.NewLine;
            File.WriteAllText(Options.DestinationImageInfoPath, destinationContents);

            return Task.CompletedTask;
        }
    }
}
