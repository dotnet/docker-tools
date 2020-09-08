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

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class BuildCommand : DockerRegistryCommand<BuildOptions>
    {
        private readonly IDockerService dockerService;
        private readonly ILoggerService loggerService;
        private readonly IGitService gitService;
        private readonly ImageDigestCache imageDigestCache;
        private readonly List<TagInfo> builtTags = new List<TagInfo>();

        private ImageArtifactDetails? imageArtifactDetails;

        [ImportingConstructor]
        public BuildCommand(IDockerService dockerService, ILoggerService loggerService, IGitService gitService)
        {
            this.imageDigestCache = new ImageDigestCache(dockerService);
            this.dockerService = new DockerServiceCache(dockerService ?? throw new ArgumentNullException(nameof(dockerService)));
            this.loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            this.gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        }

        public override Task ExecuteAsync()
        {
            if (Options.ImageInfoOutputPath != null)
            {
                imageArtifactDetails = new ImageArtifactDetails();
            }

            PullBaseImages();

            ExecuteWithUser(() =>
            {
                BuildImages();

                if (builtTags.Any())
                {
                    PushImages();
                }

                PublishImageInfo();
            });
            
            WriteBuildSummary();

            return Task.CompletedTask;
        }

        private void PublishImageInfo()
        {
            if (String.IsNullOrEmpty(Options.ImageInfoOutputPath))
            {
                return;
            }

            if (String.IsNullOrEmpty(Options.SourceRepoUrl))
            {
                throw new InvalidOperationException("Source repo URL must be provided when outputting to an image info file.");
            }

            Dictionary<string, PlatformData> platformDataByTag = new Dictionary<string, PlatformData>();
            foreach (PlatformData platformData in GetBuiltPlatforms())
            {
                foreach (TagInfo tag in platformData.PlatformInfo.Tags)
                {
                    platformDataByTag.Add(tag.FullyQualifiedName, platformData);
                }
            }

            foreach (PlatformData platform in GetBuiltPlatforms())
            {
                PlatformInfo manifestPlatform = Manifest.GetFilteredPlatforms()
                   .First(manifestPlatform => platform.Equals(manifestPlatform));

                foreach (TagInfo tag in GetPushTags(platform.PlatformInfo.Tags))
                {
                    if (Options.IsPushEnabled)
                    {
                        SetPlatformDataDigest(platform, manifestPlatform, tag.FullyQualifiedName);
                        SetPlatformDataBaseDigest(platform, platformDataByTag);
                    }

                    SetPlatformDataCreatedDate(platform, tag.FullyQualifiedName);
                    platform.CommitUrl = gitService.GetDockerfileCommitUrl(manifestPlatform, Options.SourceRepoUrl);
                }
            }

            string imageInfoString = JsonHelper.SerializeObject(imageArtifactDetails);
            File.WriteAllText(Options.ImageInfoOutputPath, imageInfoString);
        }

        private void SetPlatformDataCreatedDate(PlatformData platform, string tag)
        {
            DateTime createdDate = this.dockerService.GetCreatedDate(tag, Options.IsDryRun).ToUniversalTime();
            if (platform.Created != default && platform.Created != createdDate)
            {
                // All of the tags associated with the platform should have the same Created date
                throw new InvalidOperationException(
                    $"Tag '{tag}' has a Created date that differs from the corresponding image's Created date value of '{platform.Created}'.");
            }

            platform.Created = createdDate;
        }

        private static void SetPlatformDataBaseDigest(PlatformData platform, Dictionary<string, PlatformData> platformDataByTag)
        {
            if (platform.BaseImageDigest == null && platform.PlatformInfo.FinalStageFromImage != null)
            {
                if (!platformDataByTag.TryGetValue(platform.PlatformInfo.FinalStageFromImage, out PlatformData? basePlatformData))
                {
                    throw new InvalidOperationException(
                        $"Unable to find platform data for tag '{platform.PlatformInfo.FinalStageFromImage}'. " +
                        "It's likely that the platforms are not ordered according to dependency.");
                }

                if (basePlatformData.Digest == null)
                {
                    throw new InvalidOperationException($"Digest for platform '{basePlatformData.GetIdentifier()}' has not been calculated yet.");
                }

                platform.BaseImageDigest = basePlatformData.Digest;
            }
        }

        private void SetPlatformDataDigest(PlatformData platform, PlatformInfo manifestPlatform, string tag)
        {
            // The digest of an image that is pushed to ACR is guaranteed to be the same when transferred to MCR.
            string digest = imageDigestCache.GetImageDigest(tag, Options.IsDryRun);
            if (digest != null)
            {
                digest = DockerHelper.GetDigestString(manifestPlatform.FullRepoModelName, DockerHelper.GetDigestSha(digest));
            }

            if (platform.Digest != null && platform.Digest != digest)
            {
                // Pushing the same image with different tags should result in the same digest being output
                throw new InvalidOperationException(
                    $"Tag '{tag}' was pushed with a resulting digest value that differs from the corresponding image's digest value." +
                    Environment.NewLine +
                    $"\tDigest value from image info: {platform.Digest}{Environment.NewLine}" +
                    $"\tDigest value retrieved from query: {digest}");
            }

            platform.Digest = digest;
        }

        private void BuildImages()
        {
            this.loggerService.WriteHeading("BUILDING IMAGES");

            ImageArtifactDetails? srcImageArtifactDetails = null;
            if (Options.ImageInfoSourcePath != null)
            {
                srcImageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoSourcePath, Manifest);
            }

            foreach (RepoInfo repoInfo in Manifest.FilteredRepos)
            {
                RepoData? repoData = CreateRepoData(repoInfo);
                RepoData? srcRepoData = srcImageArtifactDetails?.Repos.FirstOrDefault(srcRepo => srcRepo.Repo == repoInfo.Name);

                foreach (ImageInfo image in repoInfo.FilteredImages)
                {
                    ImageData? imageData = CreateImageData(image);
                    repoData?.Images.Add(imageData);

                    ImageData? srcImageData = srcRepoData?.Images.FirstOrDefault(srcImage => srcImage.ManifestImage == image);

                    foreach (PlatformInfo platform in image.FilteredPlatforms)
                    {
                        // Tag the built images with the shared tags as well as the platform tags.
                        // Some tests and image FROM instructions depend on these tags.
                        
                        IEnumerable<TagInfo> allTagInfos = platform.Tags
                            .Concat(image.SharedTags)
                            .ToList();

                        builtTags.AddRange(allTagInfos);

                        IEnumerable<string> allTags = allTagInfos
                            .Select(tag => tag.FullyQualifiedName)
                            .ToList();

                        PlatformData? platformData = CreatePlatformData(image, platform);
                        imageData?.Platforms.Add(platformData);

                        bool isCachedImage = false;
                        if (!Options.NoCache && srcImageData != null)
                        {
                            PlatformData? srcPlatformData = srcImageData.Platforms.FirstOrDefault(srcPlatform => srcPlatform.Equals(platform));
                            // If this Dockerfile has been built and published before
                            if (srcPlatformData != null)
                            {
                                isCachedImage = CheckForCachedImage(platform, srcPlatformData, allTags);

                                if (platformData != null)
                                {
                                    platformData.IsCached = isCachedImage;
                                    if (isCachedImage)
                                    {
                                        platformData.BaseImageDigest = srcPlatformData.BaseImageDigest;
                                    }
                                }
                            }
                        }

                        if (!isCachedImage)
                        {
                            BuildImage(platform, allTags);

                            if (platformData != null)
                            {
                                platformData.BaseImageDigest =
                                   imageDigestCache.GetImageDigest(platform.FinalStageFromImage, Options.IsDryRun);
                            }
                        }
                    }
                }

                if (repoData?.Images.Any() == true)
                {
                    imageArtifactDetails?.Repos.Add(repoData);
                }
            }
        }

        private RepoData? CreateRepoData(RepoInfo repoInfo) =>
            Options.ImageInfoOutputPath is null ? null :
                new RepoData
                {
                    Repo = repoInfo.Name
                };

        private PlatformData? CreatePlatformData(ImageInfo image, PlatformInfo platform)
        {
            if (Options.ImageInfoOutputPath is null)
            {
                return null;
            }

            PlatformData? platformData = PlatformData.FromPlatformInfo(platform, image);
            platformData.SimpleTags = GetPushTags(platform.Tags)
                .Select(tag => tag.Name)
                .OrderBy(name => name)
                .ToList();

            return platformData;
        }

        private ImageData? CreateImageData(ImageInfo image)
        {
            ImageData? imageData = Options.ImageInfoOutputPath is null ? null :
                new ImageData
                {
                    ProductVersion = image.ProductVersion
                };

            if (image.SharedTags.Any() && imageData != null)
            {
                imageData.Manifest = new ManifestData
                {
                    SharedTags = image.SharedTags
                        .Select(tag => tag.Name)
                        .ToList()
                };
            }

            return imageData;
        }

        private void BuildImage(PlatformInfo platform, IEnumerable<string> allTags)
        {
            bool createdPrivateDockerfile = UpdateDockerfileFromCommands(platform, out string dockerfilePath);

            try
            {
                InvokeBuildHook("pre-build", platform.BuildContextPath);

                string buildOutput = this.dockerService.BuildImage(
                    dockerfilePath,
                    platform.BuildContextPath,
                    allTags,
                    platform.BuildArgs,
                    Options.IsRetryEnabled,
                    Options.IsDryRun);

                if (!Options.IsSkipPullingEnabled && !Options.IsDryRun && buildOutput.Contains("Pulling from"))
                {
                    throw new InvalidOperationException(
                        "Build resulted in a base image being pulled. All image pulls should be done as a pre-build step. " +
                        "Any other image that's not accounted for means there's some kind of mistake in how things are " +
                        "configured or a bug in the code.");
                }

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
        }

        private bool CheckForCachedImage(PlatformInfo platform, PlatformData srcPlatformData, IEnumerable<string> allTags)
        {
            this.loggerService.WriteMessage($"Checking for cached image for '{platform.DockerfilePathRelativeToManifest}'");

            // If the previously published image was based on an image that is still the latest version AND
            // the Dockerfile hasn't changed since it was last published
            if (IsBaseImageDigestUpToDate(platform, srcPlatformData) &&
                IsDockerfileUpToDate(platform, srcPlatformData) &&
                IsFullyQualifiedDigest(srcPlatformData))
            {
                this.loggerService.WriteMessage();
                this.loggerService.WriteMessage("CACHE HIT");
                this.loggerService.WriteMessage();

                // Pull the image instead of building it
                this.dockerService.PullImage(srcPlatformData.Digest, Options.IsDryRun);

                // Tag the image as if it were locally built so that subsequent built images can reference it
                Parallel.ForEach(allTags, tag =>
                {
                    this.dockerService.CreateTag(srcPlatformData.Digest, tag, Options.IsDryRun);

                    // Populate the digest cache with the known digest value for the tags assigned to the image.
                    // This is needed in order to prevent a call to the manifest tool to get the digest for these tags
                    // because they haven't yet been pushed to staging by that time.
                    this.imageDigestCache.AddDigest(tag, srcPlatformData.Digest);
                });

                return true;
            }

            this.loggerService.WriteMessage("CACHE MISS");
            this.loggerService.WriteMessage();

            return false;
        }

        // TODO: This check can be removed once all digests in the image info file have been updated to be fully-qualified
        private bool IsFullyQualifiedDigest(PlatformData srcPlatformData)
        {
            bool isFullyQualifiedSourceDigest = !srcPlatformData.Digest.StartsWith("sha256:");
            this.loggerService.WriteMessage();
            this.loggerService.WriteMessage($"Is source digest '{srcPlatformData.Digest}' fully qualified: {isFullyQualifiedSourceDigest}");
            return isFullyQualifiedSourceDigest;
        }

        private bool IsDockerfileUpToDate(PlatformInfo platform, PlatformData srcPlatformData)
        {
            string currentCommitUrl = this.gitService.GetDockerfileCommitUrl(platform, Options.SourceRepoUrl);
            bool commitShaMatches = srcPlatformData.CommitUrl.Equals(currentCommitUrl, StringComparison.OrdinalIgnoreCase);

            this.loggerService.WriteMessage();
            this.loggerService.WriteMessage($"Image info's Dockerfile commit: {srcPlatformData.CommitUrl}");
            this.loggerService.WriteMessage($"Latest Dockerfile commit: {currentCommitUrl}");
            this.loggerService.WriteMessage($"Dockerfile commits match: {commitShaMatches}");
            return commitShaMatches;
        }

        private bool IsBaseImageDigestUpToDate(PlatformInfo platform, PlatformData srcPlatformData)
        {
            string currentBaseImageDigest = imageDigestCache.GetImageDigest(platform.FinalStageFromImage, Options.IsDryRun);
            bool baseImageDigestMatches = DockerHelper.GetDigestSha(srcPlatformData.BaseImageDigest)?.Equals(
                DockerHelper.GetDigestSha(currentBaseImageDigest), StringComparison.OrdinalIgnoreCase) == true;

            this.loggerService.WriteMessage();
            this.loggerService.WriteMessage($"Image info's base image digest: {srcPlatformData.BaseImageDigest}");
            this.loggerService.WriteMessage($"Latest base image digest: {currentBaseImageDigest}");
            this.loggerService.WriteMessage($"Base image digests match: {baseImageDigestMatches}");
            return baseImageDigestMatches;
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
                this.dockerService.CreateTag(primaryTag, tag, Options.IsDryRun);
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
                Logger.WriteHeading("PULLING LATEST BASE IMAGES");
                IEnumerable<string> baseImages = Manifest.GetExternalFromImages().ToArray();
                if (baseImages.Any())
                {
                    foreach (string fromImage in baseImages)
                    {
                        dockerService.PullImage(fromImage, Options.IsDryRun);
                    }

                    IEnumerable<string> finalStageExternalFromImages = Manifest.GetFilteredPlatforms()
                        .Where(platform => !platform.IsInternalFromImage(platform.FinalStageFromImage))
                        .Select(platform => platform.FinalStageFromImage)
                        .Distinct();

                    Parallel.ForEach(finalStageExternalFromImages, fromImage =>
                    {
                        // Ensure the digest of the pulled image is retrieved right away after pulling so it's available in
                        // the DockerServiceCache for later use.  The longer we wait to get the digest after pulling, the
                        // greater change the tag could be updated resulting in a different digest returned than what was
                        // originally pulled.
                        imageDigestCache.GetImageDigest(fromImage, Options.IsDryRun);
                    });
                }
                else
                {
                    Logger.WriteMessage("No external base images to pull");
                }
            }
        }

        private IEnumerable<PlatformData> GetBuiltPlatforms() => this.imageArtifactDetails?.Repos
            .Where(repoData => repoData.Images != null)
            .SelectMany(repoData => repoData.Images)
            .SelectMany(imageData => imageData.Platforms)
            ?? Enumerable.Empty<PlatformData>();

        private void PushImages()
        {
            if (Options.IsPushEnabled)
            {
                this.loggerService.WriteHeading("PUSHING IMAGES");

                foreach (TagInfo tag in GetPushTags(builtTags))
                {
                    this.dockerService.PushImage(tag.FullyQualifiedName, Options.IsDryRun);
                }
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
                    string newFromImage = DockerHelper.ReplaceRepo(fromImage, repo.QualifiedName);
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

            if (builtTags.Any())
            {
                foreach (TagInfo tag in builtTags)
                {
                    this.loggerService.WriteMessage(tag.FullyQualifiedName);
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
#nullable restore
