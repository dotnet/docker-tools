// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildCommand : Command<BuildOptions>
    {
        public BuildCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            PullBaseImages();
            BuildImages();
            RunTests();
            PushImages();
            WriteBuildSummary();

            return Task.CompletedTask;
        }

        private void BuildImages()
        {
            Utilities.WriteHeading("BUILDING IMAGES");
            foreach (ImageInfo image in Manifest.ActiveImages)
            {
                string dockerfilePath;
                bool createdPrivateDockerfile = UpdateDockerfileFromCommands(image, out dockerfilePath);

                try
                {
                    string tagArgs = $"-t {string.Join(" -t ", image.ActiveFullyQualifiedTags)}";
                    ExecuteHelper.Execute(
                        "docker",
                        $"build {tagArgs} -f {dockerfilePath} {image.ActivePlatform.BuildContextPath}",
                        Options.IsDryRun);
                }
                finally
                {
                    if (createdPrivateDockerfile)
                    {
                        File.Delete(dockerfilePath);
                    }
                }
            }
        }

        private void PullBaseImages()
        {
            if (!Options.IsSkipPullingEnabled)
            {
                DockerHelper.PullBaseImages(Manifest, Options);
            }
        }

        private void PushImages()
        {
            if (Options.IsPushEnabled)
            {
                Utilities.WriteHeading("PUSHING IMAGES");

                if (Options.Username != null)
                {
                    DockerHelper.Login(Options.Username, Options.Password, Options.IsDryRun);
                }

                foreach (string tag in Manifest.ActivePlatformFullyQualifiedTags)
                {
                    ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", Options.IsDryRun);
                }

                if (Options.Username != null)
                {
                    ExecuteHelper.Execute("docker", $"logout", Options.IsDryRun);
                }
            }
        }

        private void RunTests()
        {
            if (!Options.IsTestRunDisabled)
            {
                Utilities.WriteHeading("TESTING IMAGES");
                IEnumerable<string> testCommands = Manifest.TestCommands
                    .Select(command => Utilities.SubstituteVariables(Options.TestVariables, command));
                foreach (string command in testCommands)
                {
                    string filename;
                    string args;

                    int firstSpaceIndex = command.IndexOf(' ');
                    if (firstSpaceIndex == -1)
                    {
                        filename = command;
                        args = null;
                    }
                    else
                    {
                        filename = command.Substring(0, firstSpaceIndex);
                        args = command.Substring(firstSpaceIndex + 1);
                    }

                    ExecuteHelper.Execute(filename, args, Options.IsDryRun);
                }
            }
        }

        private bool UpdateDockerfileFromCommands(ImageInfo image, out string dockerfilePath)
        {
            dockerfilePath = image.ActivePlatform.DockerfilePath;

            // If an alternative repo owner was specified, update the intra-repo FROM commands.
            bool updateDockerfile = !string.IsNullOrWhiteSpace(Options.RepoOwner)
                && !image.ActivePlatform.FromImages.All(Manifest.IsExternalImage);
            if (updateDockerfile)
            {
                string dockerfileContents = File.ReadAllText(dockerfilePath);

                IEnumerable<string> fromImages = image.ActivePlatform.FromImages
                    .Where(fromImage => !Manifest.IsExternalImage(fromImage));
                foreach (string fromImage in fromImages)
                {
                    Regex fromRegex = new Regex($@"FROM\s+{Regex.Escape(fromImage)}[^\S\r\n]*");
                    string newFromImage = DockerHelper.ReplaceImageOwner(fromImage, Options.RepoOwner);
                    Console.WriteLine($"Replacing FROM `{fromImage}` with `{newFromImage}`");
                    dockerfileContents = fromRegex.Replace(dockerfileContents, $"FROM {newFromImage}");
                }

                // Don't overwrite the original dockerfile - write it to a new path.
                dockerfilePath = dockerfilePath + ".temp";
                Console.WriteLine($"Writing updated Dockerfile: {dockerfilePath}");
                Console.WriteLine(dockerfileContents);
                File.WriteAllText(dockerfilePath, dockerfileContents);
            }

            return updateDockerfile;
        }

        private void WriteBuildSummary()
        {
            Utilities.WriteHeading("IMAGES BUILT");
            foreach (string tag in Manifest.ActivePlatformFullyQualifiedTags)
            {
                Console.WriteLine(tag);
            }
        }
    }
}
