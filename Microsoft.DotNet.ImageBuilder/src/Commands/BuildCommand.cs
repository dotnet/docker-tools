// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildCommand : Command<BuildOptions>
    {
        private IEnumerable<string> BuiltTags { get; set; } = Enumerable.Empty<string>();

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
                foreach (PlatformInfo platform in image.ActivePlatforms)
                {
                    bool createdPrivateDockerfile = UpdateDockerfileFromCommands(platform, out string dockerfilePath);

                    try
                    {
                        // Tag the built images with the shared tags as well as the platform tags.
                        // Some tests and image FROM instructions depend on these tags.
                        IEnumerable<string> platformTags = platform.Tags
                            .Select(tag => tag.FullyQualifiedName)
                            .ToArray();
                        IEnumerable<string> allTags = image.SharedTags
                            .Select(tag => tag.FullyQualifiedName)
                            .Concat(platformTags);
                        string tagArgs = $"-t {string.Join(" -t ", allTags)}";
                        InvokeBuildHook("pre-build", platform.BuildContextPath, Options.IsDryRun);
                        ExecuteHelper.Execute(
                            "docker",
                            $"build {tagArgs} -f {dockerfilePath} {platform.BuildContextPath}",
                            Options.IsDryRun);
                        InvokeBuildHook("post-build", platform.BuildContextPath, Options.IsDryRun);
                        BuiltTags = BuiltTags.Concat(platformTags);
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
        }

        private static void InvokeBuildHook(string hookName, string buildContextPath, bool isDryRun = false)
        {
            string hookFolder = Path.Combine(buildContextPath, "hooks");
            if (!Directory.Exists(hookFolder))
            {
                return;
            }

            string hookPath = Path.Combine(hookFolder, hookName);
            string PSScriptExtension = ".ps1";
            hookPath = File.Exists(hookPath) ? hookPath
                : File.Exists($"{hookPath}{PSScriptExtension}") ? $"{hookPath}{PSScriptExtension}" : "";
            if (hookPath != "")
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(hookPath);
                startInfo.WorkingDirectory = buildContextPath;
                if (Path.GetExtension(hookPath) == PSScriptExtension)
                {
                    startInfo.FileName = "PowerShell";
                    startInfo.Arguments = $"-NoProfile -File hooks\\{hookName}{PSScriptExtension}";
                }
                ExecuteHelper.Execute(
                    startInfo,
                    isDryRun,
                    $"Failure in {hookPath} hook");
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

                foreach (string tag in BuiltTags)
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

        private bool UpdateDockerfileFromCommands(PlatformInfo platform, out string dockerfilePath)
        {
            dockerfilePath = platform.DockerfilePath;

            // If an alternative repo owner was specified, update the intra-repo FROM commands.
            bool updateDockerfile = !string.IsNullOrWhiteSpace(Options.RepoOwner)
                && !platform.FromImages.All(Manifest.IsExternalImage);
            if (updateDockerfile)
            {
                string dockerfileContents = File.ReadAllText(dockerfilePath);

                IEnumerable<string> fromImages = platform.FromImages
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
            foreach (string tag in BuiltTags)
            {
                Console.WriteLine(tag);
            }
        }
    }
}
