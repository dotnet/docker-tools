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
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class BuildCommand : Command<BuildOptions>
    {
        private IEnumerable<TagInfo> BuiltTags { get; set; } = Enumerable.Empty<TagInfo>();

        public BuildCommand() : base()
        {
        }

        public override Task ExecuteAsync()
        {
            PullBaseImages();
            BuildImages();

            if (BuiltTags.Any())
            {
                RunTests();
                PushImages();
            }

            WriteBuildSummary();

            return Task.CompletedTask;
        }

        private void BuildImages()
        {
            Logger.WriteHeading("BUILDING IMAGES");
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
                        string tagArgs = GetDockerTagArgs(image, platformTags);
                        string buildArgs = GetDockerBuildArgs(platform);

                        InvokeBuildHook("pre-build", platform.BuildContextPath);
                        ExecuteHelper.Execute(
                            "docker",
                            $"build {tagArgs} -f {dockerfilePath}{buildArgs} {platform.BuildContextPath}",
                            Options.IsDryRun);
                        InvokeBuildHook("post-build", platform.BuildContextPath);
                        BuiltTags = BuiltTags.Concat(platform.Tags);
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

            BuiltTags = BuiltTags.ToArray();
        }

        private string GetDockerBuildArgs(PlatformInfo platform)
        {
            IEnumerable<string> buildArgs = platform.GetBuildArgs()
                .Select(buildArg => $" --build-arg {buildArg.Key}={buildArg.Value}");
            return string.Join(string.Empty, buildArgs);
        }

        private string GetDockerTagArgs(ImageInfo image, IEnumerable<string> platformTags)
        {
            IEnumerable<string> allTags = image.SharedTags
                .Select(tag => tag.FullyQualifiedName)
                .Concat(platformTags);
            return $"-t {string.Join(" -t ", allTags)}";
        }

        private void InvokeBuildHook(string hookName, string buildContextPath)
        {
            string buildHookFolder = Path.GetFullPath(Path.Combine(buildContextPath, "hooks"));
            if (!Directory.Exists(buildHookFolder))
            {
                return;
            }

            string scriptPath = Path.Combine(buildHookFolder, hookName);
            ProcessStartInfo startInfo;
            if (File.Exists(scriptPath))
            {
                startInfo = new ProcessStartInfo(scriptPath);
            }
            else
            {
                scriptPath = Path.ChangeExtension(scriptPath, ".ps1");
                if (!File.Exists(scriptPath))
                {
                    return;
                }

                startInfo = new ProcessStartInfo(
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "PowerShell" : "pwsh",
                    $"-NoProfile -File \"{scriptPath}\"");
            }

            startInfo.WorkingDirectory = buildContextPath;
            ExecuteHelper.Execute(startInfo, Options.IsDryRun, $"Failed to execute build hook '{scriptPath}'");
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
                Logger.WriteHeading("PUSHING IMAGES");

                DockerHelper.Login(Options.Username, Options.Password, Options.Server, Options.IsDryRun);
                try
                {
                    IEnumerable<string> pushTags = BuiltTags
                        .Where(tag => !tag.Model.IsLocal)
                        .Select(tag => tag.FullyQualifiedName);
                    foreach (string tag in pushTags)
                    {
                        ExecuteHelper.ExecuteWithRetry("docker", $"push {tag}", Options.IsDryRun);
                    }
                }
                finally
                {
                    DockerHelper.Logout(Options.Server, Options.IsDryRun);
                }
            }
        }

        private void RunTests()
        {
            if (!Options.IsTestRunDisabled)
            {
                Logger.WriteHeading("TESTING IMAGES");
                foreach (string command in Manifest.GetTestCommands())
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
                    Logger.WriteMessage($"Replacing FROM `{fromImage}` with `{newFromImage}`");
                    dockerfileContents = fromRegex.Replace(dockerfileContents, $"FROM {newFromImage}");
                }

                // Don't overwrite the original dockerfile - write it to a new path.
                dockerfilePath = dockerfilePath + ".temp";
                Logger.WriteMessage($"Writing updated Dockerfile: {dockerfilePath}");
                Logger.WriteMessage(dockerfileContents);
                File.WriteAllText(dockerfilePath, dockerfileContents);
            }

            return updateDockerfile;
        }

        private void WriteBuildSummary()
        {
            Logger.WriteHeading("IMAGES BUILT");

            if (BuiltTags.Any())
            {
                foreach (string tag in BuiltTags.Select(tag => tag.FullyQualifiedName))
                {
                    Logger.WriteMessage(tag);
                }
            }
            else
            {
                Logger.WriteMessage("No images built");
            }

            Logger.WriteMessage();
        }
    }
}
