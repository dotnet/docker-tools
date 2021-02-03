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

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class MergeImageInfoCommand : ManifestCommand<MergeImageInfoOptions, MergeImageInfoOptionsBuilder>
    {
        protected override string Description => "Merges the content of multiple image info files into one file";

        [ImportingConstructor]
        public MergeImageInfoCommand(IDockerService dockerService)
            : base(dockerService)
        {
        }

        protected override Task ExecuteCoreAsync()
        {
            IEnumerable<string> imageInfoFiles = Directory.EnumerateFiles(
                Options.SourceImageInfoFolderPath,
                "*.json",
                SearchOption.AllDirectories);

            List<ImageArtifactDetails> srcImageArtifactDetailsList = imageInfoFiles
                .OrderBy(file => file) // Ensure the files are ordered for testing consistency between OS's.
                .Select(imageDataPath => ImageInfoHelper.LoadFromFile(imageDataPath, Manifest))
                .ToList();

            if (!srcImageArtifactDetailsList.Any())
            {
                throw new InvalidOperationException(
                    $"No JSON files found in source folder '{Options.SourceImageInfoFolderPath}'");
            }

            ImageArtifactDetails targetImageArtifactDetails = new ImageArtifactDetails();
            foreach (ImageArtifactDetails srcImageArtifactDetails in srcImageArtifactDetailsList)
            {
                ImageInfoHelper.MergeImageArtifactDetails(srcImageArtifactDetails, targetImageArtifactDetails);
            }

            string destinationContents = JsonHelper.SerializeObject(targetImageArtifactDetails) + Environment.NewLine;
            File.WriteAllText(Options.DestinationImageInfoPath, destinationContents);

            return Task.CompletedTask;
        }
    }
}
