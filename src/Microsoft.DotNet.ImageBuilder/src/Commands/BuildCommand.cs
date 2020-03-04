// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class BuildCommand : DockerRegistryCommand<BuildOptions>
    {
        private readonly IDockerService dockerService;
        private readonly ILoggerService loggerService;
        private readonly IEnvironmentService environmentService;
        private readonly List<RepoData> repoList = new List<RepoData>();

        [ImportingConstructor]
        public BuildCommand(IDockerService dockerService, ILoggerService loggerService, IEnvironmentService environmentService)
        {
            this.dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        public override Task ExecuteAsync()
        {
            PullBaseImages();
            BuildImages();

            if (GetBuiltImages().Any())
            {
                PushImages();
            }

            PublishImageInfo();
            WriteBuildSummary();

            return Task.CompletedTask;
        }

        private void PublishImageInfo()
        {
            if (String.IsNullOrEmpty(Options.ImageInfoOutputPath))
            {
                return;
            }

            var pushedImages = this.repoList
                .Where(repoData => repoData.Images != null)
                .SelectMany(repoData => repoData.Images.Values);

            foreach (var image in pushedImages)
            {
                foreach (string tag in image.AllTags)
                {
                    // The digest of an image that is pushed to ACR is guaranteed to be the same when transferred to MCR
                    // It is output in the form of <repo>@<sha> but we only want the sha.
                    string digest = this.dockerService.GetImageDigest(tag, Options.IsDryRun);
                    digest = digest.Substring(digest.IndexOf("@") + 1);

                    if (image.Digest != null && image.Digest != digest)
                    {
                        // Pushing the same image with different tags should result in the same digest being output
                        this.loggerService.WriteError(
                            $"Tag '{tag}' was pushed with a resulting digest value that differs from the corresponding image's digest value of '{image.Digest}'.");
                        this.environmentService.Exit(1);
                    }

                    image.Digest = digest;
                }
            }

            string imageInfoString = JsonHelper.SerializeObject(repoList.OrderBy(r => r.Repo).ToArray());
            File.WriteAllText(Options.ImageInfoOutputPath, imageInfoString);
        }

        private void BuildImages()
        {
            this.loggerService.WriteHeading("BUILDING IMAGES");

            foreach (RepoInfo repoInfo in Manifest.FilteredRepos)
            {
                RepoData repoData = new RepoData
                {
                    Repo = repoInfo.Model.Name
                };
                repoList.Add(repoData);

                SortedDictionary<string, ImageData> images = new SortedDictionary<string, ImageData>();

                foreach (ImageInfo image in repoInfo.FilteredImages)
                {
                    foreach (PlatformInfo platform in image.FilteredPlatforms)
                    {
                        ImageData imageData = new ImageData();
                        images.Add(platform.DockerfilePathRelativeToManifest, imageData);

                        bool createdPrivateDockerfile = UpdateDockerfileFromCommands(platform, out string dockerfilePath);

                        IEnumerable<string> allTags;

                        try
                        {
                            InvokeBuildHook("pre-build", platform.BuildContextPath);

                            // Tag the built images with the shared tags as well as the platform tags.
                            // Some tests and image FROM instructions depend on these tags.
                            allTags = platform.Tags
                                .Concat(image.SharedTags)
                                .Select(tag => tag.FullyQualifiedName)
                                .ToList();

                            this.dockerService.BuildImage(
                                dockerfilePath,
                                platform.BuildContextPath,
                                allTags,
                                platform.BuildArgs,
                                Options.IsRetryEnabled,
                                Options.IsDryRun);

                            if (!Options.IsDryRun)
                            {
                                EnsureArchitectureMatches(platform, allTags);
                            }

                            InvokeBuildHook("post-build", platform.BuildContextPath);
                        }
                        finally
                        {
                            if (createdPrivateDockerfile)
                            {
                                File.Delete(dockerfilePath);
                            }
                        }

                        SortedDictionary<string, string> baseImageDigests = GetBaseImageDigests(platform);
                        if (baseImageDigests.Any())
                        {
                            imageData.BaseImages = baseImageDigests;
                        }

                        imageData.SimpleTags = GetPushTags(platform.Tags)
                            .Select(tag => tag.Name)
                            .OrderBy(name => name)
                            .ToList();
                        imageData.AllTags = allTags;
                    }
                }

                if (images.Any())
                {
                    repoData.Images = images;
                }
            }
        }

        private SortedDictionary<string, string> GetBaseImageDigests(PlatformInfo platform)
        {
            SortedDictionary<string, string> baseImageDigestMappings = new SortedDictionary<string, string>();
            foreach (string fromImage in platform.ExternalFromImages)
            {
                string digest = this.dockerService.GetImageDigest(fromImage, Options.IsDryRun);
                baseImageDigestMappings[fromImage] = digest;
            }

            return baseImageDigestMappings;
        }

        private void EnsureArchitectureMatches(PlatformInfo platform, IEnumerable<string> allTags)
        {
            if (platform.Model.Architecture == this.dockerService.Architecture)
            {
                return;
            }

            string primaryTag = allTags.First();
            IEnumerable<string> secondaryTags = allTags.Except(new[] { primaryTag });

            // Get the architecture from the built image's metadata
            string actualArchitecture = DockerHelper.GetImageArch(primaryTag, Options.IsDryRun);
            string expectedArchitecture = platform.Model.Architecture.GetDockerName();

            // If the architecture of the built image is what we expect, then exit the method; otherwise, continue
            // with updating the architecture metadata.
            if (String.Equals(actualArchitecture, expectedArchitecture))
            {
                return;
            }

            // Save the Docker image to a tar file
            string tempImageTar = "image.tar.gz";
            DockerHelper.SaveImage(primaryTag, tempImageTar, Options.IsDryRun);
            try
            {
                string tarContentsDirectory = "tar_contents";
                Directory.CreateDirectory(tarContentsDirectory);

                try
                {
                    // Extract the tar file to a separate directory
                    ExecuteHelper.Execute("tar", $"-xf {tempImageTar} -C {tarContentsDirectory}", Options.IsDryRun);

                    // Open the manifest to find the name of the Config json file
                    string manifestContents = File.ReadAllText(Path.Combine(tarContentsDirectory, "manifest.json"));
                    JArray manifestDoc = JArray.Parse(manifestContents);

                    if (manifestDoc.Count != 1)
                    {
                        throw new InvalidOperationException(
                            $"Only expected one element in tar archive's manifest:{Environment.NewLine}{manifestContents}");
                    }

                    // Open the Config json file and set the architecture value
                    string configPath = Path.Combine(tarContentsDirectory, manifestDoc[0]["Config"].Value<string>());
                    string configContents = File.ReadAllText(configPath);
                    JObject config = JObject.Parse(configContents);
                    config["architecture"] = expectedArchitecture;

                    // Overwrite the Config json file with the updated architecture value
                    configContents = JsonConvert.SerializeObject(config);
                    File.WriteAllText(configPath, configContents);

                    // Repackage the directory into an updated tar file
                    ExecuteHelper.Execute("tar", $"-cf {tempImageTar} -C {tarContentsDirectory} .", Options.IsDryRun);
                }
                finally
                {
                    Directory.Delete(tarContentsDirectory, recursive: true);
                }

                // Load the updated tar file back into Docker
                DockerHelper.LoadImage(tempImageTar, Options.IsDryRun);
            }
            finally
            {
                File.Delete(tempImageTar);
            }

            // Recreate the other tags so that they get the updated architecture value.
            Parallel.ForEach(secondaryTags, tag =>
            {
                DockerHelper.CreateTag(primaryTag, tag, Options.IsDryRun);
            });
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
                this.dockerService.PullBaseImages(Manifest, Options.IsDryRun);
            }
        }

        private IEnumerable<ImageData> GetBuiltImages() => this.repoList
            .Where(repoData => repoData.Images != null)
            .SelectMany(repoData => repoData.Images.Values);

        private void PushImages()
        {
            if (Options.IsPushEnabled)
            {
                this.loggerService.WriteHeading("PUSHING IMAGES");

                ExecuteWithUser(() =>
                {
                    var imagesToPush = GetBuiltImages();

                    foreach (var image in imagesToPush)
                    {
                        foreach (string tag in image.AllTags)
                        {
                            this.dockerService.PushImage(tag, Options.IsDryRun);
                        }
                    }
                });
            }
        }

        private static IEnumerable<TagInfo> GetPushTags(IEnumerable<TagInfo> buildTags) =>
            buildTags.Where(tag => !tag.Model.IsLocal);

        private bool UpdateDockerfileFromCommands(PlatformInfo platform, out string dockerfilePath)
        {
            bool updateDockerfile = false;
            dockerfilePath = platform.DockerfilePath;

            // If a repo override has been specified, update the FROM commands.
            if (platform.OverriddenFromImages.Any())
            {
                string dockerfileContents = File.ReadAllText(dockerfilePath);

                foreach (string fromImage in platform.OverriddenFromImages)
                {
                    string fromRepo = DockerHelper.GetRepo(fromImage);
                    RepoInfo repo = Manifest.FilteredRepos.First(r => r.FullModelName == fromRepo);
                    string newFromImage = DockerHelper.ReplaceRepo(fromImage, repo.Name);
                    this.loggerService.WriteMessage($"Replacing FROM `{fromImage}` with `{newFromImage}`");
                    Regex fromRegex = new Regex($@"FROM\s+{Regex.Escape(fromImage)}[^\S\r\n]*");
                    dockerfileContents = fromRegex.Replace(dockerfileContents, $"FROM {newFromImage}");
                    updateDockerfile = true;
                }

                if (updateDockerfile)
                {
                    // Don't overwrite the original dockerfile - write it to a new path.
                    dockerfilePath += ".temp";
                    this.loggerService.WriteMessage($"Writing updated Dockerfile: {dockerfilePath}");
                    this.loggerService.WriteMessage(dockerfileContents);
                    File.WriteAllText(dockerfilePath, dockerfileContents);
                }
            }

            return updateDockerfile;
        }

        private void WriteBuildSummary()
        {
            this.loggerService.WriteHeading("IMAGES BUILT");

            IEnumerable<string> builtTags = GetBuiltImages()
                .SelectMany(image => image.AllTags);

            if (builtTags.Any())
            {
                foreach (string tag in builtTags)
                {
                    this.loggerService.WriteMessage(tag);
                }
            }
            else
            {
                this.loggerService.WriteMessage("No images built");
            }

            this.loggerService.WriteMessage();
        }
    }
}
