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
        private readonly List<RepoData> reposList = new List<RepoData>();

        private IEnumerable<TagInfo> BuiltTags { get; set; } = Enumerable.Empty<TagInfo>();
        

        [ImportingConstructor]
        public BuildCommand(IDockerService dockerService)
        {
            this.dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
        }

        public override Task ExecuteAsync()
        {
            PullBaseImages();
            BuildImages();

            if (BuiltTags.Any())
            {
                PushImages();
            }

            if (!String.IsNullOrEmpty(Options.ImageInfoOutputPath))
            {
                string digestsString = JsonHelper.SerializeObject(reposList.OrderBy(r => r.Repo).ToArray());
                File.WriteAllText(Options.ImageInfoOutputPath, digestsString);
            }

            WriteBuildSummary();

            return Task.CompletedTask;
        }

        private void BuildImages()
        {
            Logger.WriteHeading("BUILDING IMAGES");

            string baseDirectory = Path.GetDirectoryName(Options.Manifest);

            foreach (RepoInfo repoInfo in Manifest.FilteredRepos)
            {
                RepoData repoData = new RepoData
                {
                    Repo = repoInfo.Model.Name
                };

                SortedDictionary<string, ImageData> images = new SortedDictionary<string, ImageData>();

                foreach (ImageInfo image in repoInfo.FilteredImages)
                {
                    foreach (PlatformInfo platform in image.FilteredPlatforms)
                    {
                        ImageData imageData = new ImageData();
                        images.Add(platform.DockerfilePath, imageData);

                        bool createdPrivateDockerfile = UpdateDockerfileFromCommands(platform, out string dockerfilePath);

                        try
                        {
                            InvokeBuildHook("pre-build", platform.BuildContextPath);

                            // Tag the built images with the shared tags as well as the platform tags.
                            // Some tests and image FROM instructions depend on these tags.
                            IEnumerable<string> allTags = platform.Tags
                                .Concat(image.SharedTags)
                                .Select(tag => tag.FullyQualifiedName);

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
                            BuiltTags = BuiltTags.Concat(platform.Tags);
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

                        imageData.SimpleTags = platform.Tags
                            .Where(tag => !tag.Model.IsLocal)
                            .Select(tag => tag.Name)
                            .OrderBy(name => name)
                            .ToList();
                    }
                }

                if (images.Any())
                {
                    repoData.Images = images;
                    reposList.Add(repoData);
                }
            }

            BuiltTags = BuiltTags.ToArray();
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

        private void PushImages()
        {
            if (Options.IsPushEnabled)
            {
                Logger.WriteHeading("PUSHING IMAGES");

                ExecuteWithUser(() =>
                {
                    var imagesToPush = this.reposList
                        .Select(repoData => new
                        {
                            Repo = repoData,
                            RepoInfo = this.Manifest.GetRepoByModelName(repoData.Repo)
                        })
                        .SelectMany(repoData =>
                            repoData.Repo.Images
                                .Select(image => new
                                {
                                    Image = image.Value,
                                    FullyQualifiedSimpleTags = image.Value.SimpleTags
                                        .Select(tag => TagInfo.GetFullyQualifiedName(repoData.RepoInfo.Name, tag))
                                }));

                    foreach (var image in imagesToPush)
                    {
                        foreach (var tag in image.FullyQualifiedSimpleTags)
                        {
                            string digest = this.dockerService.PushImage(tag, Options.IsDryRun);
                            if (image.Image.Digest != null && image.Image.Digest != digest)
                            {
                                // Pushing the same image with different tags should result in the same digest being output
                                Logger.WriteError($"Tag '{tag}' was pushed with a resulting digest value that differs from the corresponding image's digest value of '{image.Image.Digest}'.");
                                Environment.Exit(1);
                            }

                            image.Image.Digest = digest;
                        }
                    }
                });
            }
        }

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
                    Logger.WriteMessage($"Replacing FROM `{fromImage}` with `{newFromImage}`");
                    Regex fromRegex = new Regex($@"FROM\s+{Regex.Escape(fromImage)}[^\S\r\n]*");
                    dockerfileContents = fromRegex.Replace(dockerfileContents, $"FROM {newFromImage}");
                    updateDockerfile = true;
                }

                if (updateDockerfile)
                {
                    // Don't overwrite the original dockerfile - write it to a new path.
                    dockerfilePath += ".temp";
                    Logger.WriteMessage($"Writing updated Dockerfile: {dockerfilePath}");
                    Logger.WriteMessage(dockerfileContents);
                    File.WriteAllText(dockerfilePath, dockerfileContents);
                }
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
