// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class BuildCommand : DockerRegistryCommand<BuildOptions, BuildOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILoggerService _loggerService;
        private readonly IGitService _gitService;
        private readonly IProcessService _processService;
        private readonly ICopyImageService _copyImageService;
        private readonly ImageDigestCache _imageDigestCache;
        private readonly List<TagInfo> _processedTags = new List<TagInfo>();
        private readonly HashSet<PlatformData> _builtPlatforms = new();

        // Metadata about Dockerfiles whose images have been retrieved from the cache
        private readonly Dictionary<string, PlatformData> _cachedPlatforms = new Dictionary<string, PlatformData>();

        /// <summary>
        /// Maps a source digest from the image info file to the corresponding digest in the copied location for image caching.
        /// This is specifically needed to support shared Dockerfile scenarios.
        /// </summary>
        private readonly Dictionary<string, string> _sourceDigestCopyLocationMapping = new();

        private ImageArtifactDetails? _imageArtifactDetails;

        [ImportingConstructor]
        public BuildCommand(IDockerService dockerService, ILoggerService loggerService, IGitService gitService,
            IProcessService processService, ICopyImageService copyImageService)
        {
            _imageDigestCache = new ImageDigestCache(dockerService);
            _dockerService = new DockerServiceCache(dockerService ?? throw new ArgumentNullException(nameof(dockerService)));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _copyImageService = copyImageService ?? throw new ArgumentNullException(nameof(copyImageService));
        }

        protected override string Description => "Builds Dockerfiles";

        public override async Task ExecuteAsync()
        {
            Options.BaseImageOverrideOptions.Validate();

            if (Options.ImageInfoOutputPath != null)
            {
                _imageArtifactDetails = new ImageArtifactDetails();
            }

            await ExecuteWithUserAsync(async () =>
            {
                await PullBaseImagesAsync();

                await BuildImagesAsync();

                if (_processedTags.Any() || _cachedPlatforms.Any())
                {
                    PushImages();
                }

                await PublishImageInfoAsync();
            });

            WriteBuildSummary();
            WriteBuiltImagesToOutputVar();
        }

        private void WriteBuiltImagesToOutputVar()
        {
            if (!string.IsNullOrEmpty(Options.OutputVariableName))
            {
                IEnumerable<string> builtDigests = _builtPlatforms
                    .Select(platform => DockerHelper.GetDigestString(platform.PlatformInfo!.RepoName, DockerHelper.GetDigestSha(platform.Digest)))
                    .Distinct();
                _loggerService.WriteMessage(
                    PipelineHelper.FormatOutputVariable(
                        Options.OutputVariableName,
                        string.Join(',', builtDigests)));
            }
        }

        private async Task PublishImageInfoAsync()
        {
            if (string.IsNullOrEmpty(Options.ImageInfoOutputPath))
            {
                return;
            }

            if (string.IsNullOrEmpty(Options.SourceRepoUrl))
            {
                throw new InvalidOperationException("Source repo URL must be provided when outputting to an image info file.");
            }

            Dictionary<string, PlatformData> platformDataByTag = new Dictionary<string, PlatformData>();
            foreach (PlatformData platformData in GetProcessedPlatforms())
            {
                if (platformData.PlatformInfo is not null)
                {
                    foreach (TagInfo tag in platformData.PlatformInfo.Tags)
                    {
                        platformDataByTag.Add(tag.FullyQualifiedName, platformData);
                    }
                }
            }

            IEnumerable<PlatformData> processedPlatforms = GetProcessedPlatforms();
            List<PlatformData> platformsWithNoPushTags = new List<PlatformData>();

            foreach (PlatformData platform in processedPlatforms)
            {
                IEnumerable<TagInfo> pushTags = GetPushTags(platform.PlatformInfo?.Tags ?? Enumerable.Empty<TagInfo>());

                foreach (TagInfo tag in pushTags)
                {
                    if (Options.IsPushEnabled)
                    {
                        await SetPlatformDataDigestAsync(platform, tag.FullyQualifiedName);
                        SetPlatformDataBaseDigest(platform, platformDataByTag);
                        await SetPlatformDataLayersAsync(platform, tag.FullyQualifiedName);
                    }

                    SetPlatformDataCreatedDate(platform, tag.FullyQualifiedName);
                }

                if (!pushTags.Any())
                {
                    platformsWithNoPushTags.Add(platform);
                }

                platform.CommitUrl = _gitService.GetDockerfileCommitUrl(platform.PlatformInfo, Options.SourceRepoUrl);
            }

            // Some platforms do not have concrete tags. In such cases, they must be duplicates of a platform in a different
            // image which does have a concrete tag. For these platforms that do not have concrete tags, we are unable to
            // lookup digest/created info based on their tag. Instead, we find the matching platform which does have that info
            // set (as a result of having a concrete tag) and copy its values.
            foreach (PlatformData platform in platformsWithNoPushTags)
            {
                PlatformData matchingBuiltPlatform = processedPlatforms.First(builtPlatform =>
                    GetPushTags(builtPlatform.PlatformInfo?.Tags ?? Enumerable.Empty<TagInfo>()).Any() &&
                    platform.ImageInfo is not null &&
                    platform.PlatformInfo is not null &&
                    builtPlatform.ImageInfo is not null &&
                    builtPlatform.PlatformInfo is not null &&
                    PlatformInfo.AreMatchingPlatforms(platform.ImageInfo, platform.PlatformInfo, builtPlatform.ImageInfo, builtPlatform.PlatformInfo));

                platform.Digest = matchingBuiltPlatform.Digest;
                platform.Created = matchingBuiltPlatform.Created;
            }

            string imageInfoString = JsonHelper.SerializeObject(_imageArtifactDetails);
            File.WriteAllText(Options.ImageInfoOutputPath, imageInfoString);
        }

        private void SetPlatformDataCreatedDate(PlatformData platform, string tag)
        {
            DateTime createdDate = _dockerService.GetCreatedDate(tag, Options.IsDryRun).ToUniversalTime();
            if (platform.Created != default && platform.Created != createdDate)
            {
                // All of the tags associated with the platform should have the same Created date
                throw new InvalidOperationException(
                    $"Tag '{tag}' has a Created date that differs from the corresponding image's Created date value of '{platform.Created}'.");
            }

            platform.Created = createdDate;
        }

        private void SetPlatformDataBaseDigest(PlatformData platform, Dictionary<string, PlatformData> platformDataByTag)
        {
            string? baseImageDigest = platform.BaseImageDigest;
            if (platform.BaseImageDigest is null && platform.PlatformInfo?.FinalStageFromImage is not null)
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

                baseImageDigest = basePlatformData.Digest;
            }

            if (platform.PlatformInfo?.FinalStageFromImage is not null && baseImageDigest is not null)
            {
                baseImageDigest = DockerHelper.GetDigestString(
                    DockerHelper.GetRepo(GetFromImagePublicTag(platform.PlatformInfo.FinalStageFromImage)),
                    DockerHelper.GetDigestSha(baseImageDigest));
            }

            platform.BaseImageDigest = baseImageDigest;
        }

        private async Task SetPlatformDataLayersAsync(PlatformData platform, string tag)
        {
            if (platform.Layers == null || !platform.Layers.Any())
            {
                platform.Layers = (await _dockerService.GetImageManifestLayersAsync(tag, Options.CredentialsOptions, Options.IsDryRun)).ToList();
            }
        }

        private async Task SetPlatformDataDigestAsync(PlatformData platform, string tag)
        {
            // The digest of an image that is pushed to ACR is guaranteed to be the same when transferred to MCR.
            string? digest = await _imageDigestCache.GetImageDigestAsync(tag, Options.CredentialsOptions, Options.IsDryRun);
            if (digest is not null && platform.PlatformInfo is not null)
            {
                digest = DockerHelper.GetDigestString(platform.PlatformInfo.FullRepoModelName, DockerHelper.GetDigestSha(digest));
            }

            if (!string.IsNullOrEmpty(platform.Digest) && platform.Digest != digest)
            {
                // Pushing the same image with different tags should result in the same digest being output
                throw new InvalidOperationException(
                    $"Tag '{tag}' was pushed with a resulting digest value that differs from the corresponding image's digest value." +
                    Environment.NewLine +
                    $"\tDigest value from image info: {platform.Digest}{Environment.NewLine}" +
                    $"\tDigest value retrieved from query: {digest}");
            }

            if (digest is null)
            {
                throw new InvalidOperationException($"Unable to retrieve digest for Dockerfile '{platform.Dockerfile}'.");
            }

            platform.Digest = digest;
        }

        private async Task BuildImagesAsync()
        {
            _loggerService.WriteHeading("BUILDING IMAGES");

            ImageArtifactDetails? srcImageArtifactDetails = null;
            if (Options.ImageInfoSourcePath != null)
            {
                srcImageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoSourcePath, Manifest, skipManifestValidation: true);
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

                        IEnumerable<string> allTags = allTagInfos
                            .Select(tag => tag.FullyQualifiedName)
                            .ToList();

                        PlatformData? platformData = CreatePlatformData(image, platform);
                        imageData?.Platforms.Add(platformData);

                        bool isCachedImage = !Options.NoCache &&
                            await CheckForCachedImageAsync(srcImageData, repoInfo, platform, allTagInfos, platformData);

                        if (!isCachedImage)
                        {
                            _processedTags.AddRange(allTagInfos);

                            BuildImage(platform, allTags);

                            if (platformData is not null)
                            {
                                _builtPlatforms.Add(platformData);
                            }

                            if (platformData is not null && platform.FinalStageFromImage is not null)
                            {
                                platformData.BaseImageDigest =
                                   await _imageDigestCache.GetImageDigestAsync(
                                       GetFromImageLocalTag(platform.FinalStageFromImage), Options.CredentialsOptions, Options.IsDryRun);
                            }
                        }
                    }
                }

                if (repoData?.Images.Any() == true)
                {
                    _imageArtifactDetails?.Repos.Add(repoData);
                }
            }
        }

        private async Task<bool> CheckForCachedImageAsync(
            ImageData? srcImageData, RepoInfo repo, PlatformInfo platform, IEnumerable<TagInfo> allTags, PlatformData? platformData)
        {
            PlatformData? srcPlatformData = srcImageData?.Platforms.FirstOrDefault(srcPlatform => srcPlatform.PlatformInfo == platform);

            string cacheKey = GetBuildCacheKey(platform);
            if (platformData != null && _cachedPlatforms.TryGetValue(cacheKey, out PlatformData? cachedPlatform))
            {
                await OnCacheHitAsync(repo, allTags, pullImage: false, cachedPlatform.Digest);
                CopyPlatformDataFromCachedPlatform(platformData, cachedPlatform);
                platformData.IsUnchanged = srcPlatformData != null &&
                    CachedPlatformHasAllTagsPublished(srcPlatformData);
                return true;
            }

            bool isCachedImage = false;

            // If this Dockerfile has been built and published before
            if (srcPlatformData != null)
            {
                isCachedImage = await CheckForCachedImageFromImageInfoAsync(repo, platform, srcPlatformData, allTags);
                if (platformData != null)
                {
                    platformData.IsUnchanged = isCachedImage &&
                        CachedPlatformHasAllTagsPublished(srcPlatformData);
                    if (isCachedImage)
                    {
                        CopyPlatformDataFromCachedPlatform(platformData, srcPlatformData);
                        _cachedPlatforms[cacheKey] = srcPlatformData;
                    }
                }
            }

            return isCachedImage;
        }

        private void CopyPlatformDataFromCachedPlatform(PlatformData dstPlatform, PlatformData srcPlatform)
        {
            // When a cache hit occurs for a Dockerfile, we want to transfer some of the metadata about the previously
            // published image so we don't need to recalculate it again.
            dstPlatform.BaseImageDigest = srcPlatform.BaseImageDigest;
            dstPlatform.Layers = new List<string>(srcPlatform.Layers);
        }

        private bool CachedPlatformHasAllTagsPublished(PlatformData srcPlatformData) =>
            (srcPlatformData.PlatformInfo?.Tags ?? Enumerable.Empty<TagInfo>())
                .Where(tag => !tag.Model.IsLocal)
                .Select(tag => tag.Name)
                .AreEquivalent(srcPlatformData.SimpleTags);

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

        private void ValidatePlatformIsCompatibleWithBaseImage(PlatformInfo platform)
        {
            if (platform.FinalStageFromImage is null || Options.SkipPlatformCheck)
            {
                return;
            }

            string baseImageTag = GetFromImageLocalTag(platform.FinalStageFromImage);

            // Base image should already be pulled or built so it's ok to inspect it
            (Models.Manifest.Architecture arch, string? variant) =
                _dockerService.GetImageArch(baseImageTag, Options.IsDryRun);

            if (platform.Model.Architecture != arch || platform.Model.Variant != variant)
            {
                throw new InvalidOperationException(
                    $"Platform '{platform.DockerfilePathRelativeToManifest}' is configured with an architecture that is not compatible with " +
                    $"the base image '{baseImageTag}':" + Environment.NewLine +
                    "Manifest platform:" + Environment.NewLine +
                        $"\tArchitecture: {platform.Model.Architecture}" + Environment.NewLine +
                        $"\tVariant: {platform.Model.Variant}" + Environment.NewLine +
                    "Base image:" + Environment.NewLine +
                        $"\tArchitecture: {arch}" + Environment.NewLine +
                        $"\tVariant: {variant}");
            }
        }

        private void BuildImage(PlatformInfo platform, IEnumerable<string> allTags)
        {
            ValidatePlatformIsCompatibleWithBaseImage(platform);

            bool createdPrivateDockerfile = UpdateDockerfileFromCommands(platform, out string dockerfilePath);

            try
            {
                InvokeBuildHook("pre-build", platform.BuildContextPath);

                IEnumerable<string> additionalArgs = new List<string>();

                if (Options.SecretInfo != null)
                {
                    foreach ((string Id, string Src) secretInfo in Options.SecretInfo)
                    {
                        additionalArgs = additionalArgs.Append($"--secret id={secretInfo.Id},src={secretInfo.Src}");
                    }
                }

                string? buildOutput = _dockerService.BuildImage(
                    dockerfilePath,
                    platform.BuildContextPath,
                    platform.PlatformLabel,
                    allTags,
                    GetBuildArgs(platform),
                    additionalArgs,
                    Options.IsRetryEnabled,
                    Options.IsDryRun);

                if (!Options.IsSkipPullingEnabled && !Options.IsDryRun && buildOutput?.Contains("Pulling from") == true)
                {
                    throw new InvalidOperationException(
                        "Build resulted in a base image being pulled. All image pulls should be done as a pre-build step. " +
                        "Any other image that's not accounted for means there's some kind of mistake in how things are " +
                        "configured or a bug in the code.");
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

        private Dictionary<string, string?> GetBuildArgs(PlatformInfo platform)
        {
            // Manifest-defined build args take precendence over build args defined in the build options
            Dictionary<string, string?> buildArgs = new(Options.BuildArgs.Cast<KeyValuePair<string, string?>>());
            foreach (KeyValuePair<string, string?> kvp in platform.BuildArgs)
            {
                buildArgs[kvp.Key] = kvp.Value;
            }

            return buildArgs;
        }

        private async Task<bool> CheckForCachedImageFromImageInfoAsync(
            RepoInfo repo, PlatformInfo platform, PlatformData srcPlatformData, IEnumerable<TagInfo> allTags)
        {
            _loggerService.WriteMessage($"Checking for cached image for '{platform.DockerfilePathRelativeToManifest}'");

            // If the previously published image was based on an image that is still the latest version AND
            // the Dockerfile hasn't changed since it was last published
            if (await IsBaseImageDigestUpToDateAsync(platform, srcPlatformData) && IsDockerfileUpToDate(platform, srcPlatformData))
            {
                await OnCacheHitAsync(repo, allTags, pullImage: true, sourceDigest: srcPlatformData.Digest);
                return true;
            }

            _loggerService.WriteMessage("CACHE MISS");
            _loggerService.WriteMessage();

            return false;
        }

        private async Task OnCacheHitAsync(RepoInfo repo, IEnumerable<TagInfo> allTags, bool pullImage, string sourceDigest)
        {
            _loggerService.WriteMessage();
            _loggerService.WriteMessage("CACHE HIT");
            _loggerService.WriteMessage();

            // When a cache hit occurs on an image, we copy the image from its source location (e.g. mcr.microsoft.com) to its
            // destination location (e.g. staging repo in ACR). Copying only occurs if push is enabled since it will result in
            // a write operation on the registry and is essentially a push. We then pull the image by its digest. If push is enabled
            // and the image was copied, we pull from the destination of the copy; otherwise, we pull directly from the source.
            // The pulled image is then tagged with the same tags it would be tagged with had it been built locally. This allows
            // dependent Dockerfiles that reference those tags to seamlessly consume the pulled image.

            string copiedSourceDigest = sourceDigest;
            if (Options.IsPushEnabled)
            {
                copiedSourceDigest = await CopyCachedImage(allTags, sourceDigest);
            }

            // Pull the image instead of building it
            if (pullImage)
            {
                // Don't need to provide the platform because we're pulling by digest. No need to worry about multi-arch tags.
                _dockerService.PullImage(copiedSourceDigest, null, Options.IsDryRun);
                _sourceDigestCopyLocationMapping[sourceDigest] = copiedSourceDigest;
            }

            // Tag the image as if it were locally built so that subsequent built images can reference it
            foreach (TagInfo tag in allTags)
            {
                if (!_sourceDigestCopyLocationMapping.TryGetValue(sourceDigest, out string? resolvedSourceDigest))
                {
                    throw new InvalidOperationException("Digest should be mapped by this point");
                }
                _dockerService.CreateTag(resolvedSourceDigest, tag.FullyQualifiedName, Options.IsDryRun);

                // Rewrite the digest to match the repo of the tags being associated with it. This is necessary
                // in order to handle scenarios where shared Dockerfiles are being used across different repositories.
                // In that scenario, the digest that is retrieved will be based on the repo of the first repository
                // encountered. For subsequent cache hits on different repositories, we need to prepopulate the digest
                // cache with a digest value that would correspond to that repository, not the original repository.
                string newDigest = DockerHelper.GetImageName(
                    Manifest.Model.Registry, repo.Model.Name, digest: DockerHelper.GetDigestSha(resolvedSourceDigest));

                // Populate the digest cache with the known digest value for the tags assigned to the image.
                // This is needed in order to prevent a call to the manifest tool to get the digest for these tags
                // because they haven't yet been pushed to staging by that time.
                _imageDigestCache.AddDigest(tag.FullyQualifiedName, newDigest);
            }
        }

        private async Task<string> CopyCachedImage(IEnumerable<TagInfo> allTags, string sourceDigest)
        {
            if (string.IsNullOrEmpty(Options.Subscription))
            {
                throw new InvalidDataException("Subscription option must be set.");
            }

            if (string.IsNullOrEmpty(Options.ResourceGroup))
            {
                throw new InvalidDataException("Resource group option must be set.");
            }

            string[] destTags = GetPushTags(allTags)
                                .Select(tagInfo => DockerHelper.TrimRegistry(tagInfo.FullyQualifiedName))
                                .ToArray();
            string? srcRegistry = DockerHelper.GetRegistry(sourceDigest);
            await _copyImageService.ImportImageAsync(Options.Subscription, Options.ResourceGroup,
                    Options.ServicePrincipal, destTags, Manifest.Registry, DockerHelper.TrimRegistry(sourceDigest, srcRegistry),
                    srcRegistry);

            // Redefine the source digest to be from the destination of the copy, not the source. The canonical scenario
            // here is to copy the cached image from MCR to the staging location in an ACR. This allows test jobs to always pull
            // from that staging location, not knowing whether it ended up there as a built image or a cached image.
            string destRepo = DockerHelper.GetRepo(DockerHelper.GetRepo(destTags.First()));
            sourceDigest = DockerHelper.GetImageName(Manifest.Registry, destRepo, digest: DockerHelper.GetDigestSha(sourceDigest));
            return sourceDigest;
        }

        private bool IsDockerfileUpToDate(PlatformInfo platform, PlatformData srcPlatformData)
        {
            string currentCommitUrl = _gitService.GetDockerfileCommitUrl(platform, Options.SourceRepoUrl);
            bool commitShaMatches = false;
            if (srcPlatformData.CommitUrl is not null)
            {
                commitShaMatches = srcPlatformData.CommitUrl.Equals(currentCommitUrl, StringComparison.OrdinalIgnoreCase);
            }

            _loggerService.WriteMessage();
            _loggerService.WriteMessage($"Image info's Dockerfile commit: {srcPlatformData.CommitUrl}");
            _loggerService.WriteMessage($"Latest Dockerfile commit: {currentCommitUrl}");
            _loggerService.WriteMessage($"Dockerfile commits match: {commitShaMatches}");
            return commitShaMatches;
        }

        private async Task<bool> IsBaseImageDigestUpToDateAsync(PlatformInfo platform, PlatformData srcPlatformData)
        {
            _loggerService.WriteMessage();

            if (platform.FinalStageFromImage is null)
            {
                _loggerService.WriteMessage($"Image does not have a base image. By default, it is considered up-to-date.");
                return true;
            }

            string? currentBaseImageDigest = await _imageDigestCache.GetImageDigestAsync(
                GetFromImageLocalTag(platform.FinalStageFromImage), Options.CredentialsOptions, Options.IsDryRun);

            string? baseSha = srcPlatformData.BaseImageDigest is not null ? DockerHelper.GetDigestSha(srcPlatformData.BaseImageDigest) : null;
            string? currentSha = currentBaseImageDigest is not null ? DockerHelper.GetDigestSha(currentBaseImageDigest) : null;
            bool baseImageDigestMatches = baseSha?.Equals(currentSha, StringComparison.OrdinalIgnoreCase) == true;

            _loggerService.WriteMessage($"Image info's base image digest: {srcPlatformData.BaseImageDigest}");
            _loggerService.WriteMessage($"Latest base image digest: {currentBaseImageDigest}");
            _loggerService.WriteMessage($"Base image digests match: {baseImageDigestMatches}");
            return baseImageDigestMatches;
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
            _processService.Execute(startInfo, Options.IsDryRun, $"Failed to execute build hook '{scriptPath}'");
        }

        /// <summary>
        /// Returns the tag to use for pulling the image of a FROM instruction.
        /// </summary>
        /// <param name="fromImage">Tag of the FROM image.</param>
        private string GetFromImagePullTag(string fromImage) =>
            // Provides the raw registry value from the manifest (e.g. mcr.microsoft.com). This accounts for images that
            // are classified as external within the model but they are owned internally and not mirrored. An example of
            // this is sample images. By comparing their base image tag to that raw registry value from the manifest, we
            // can know that these are owned internally and not to attempt to pull them from the mirror location.
            GetFromImageTag(fromImage, Manifest.Model.Registry);

        /// <summary>
        /// Returns the tag to use for interacting with the image of a FROM instruction that has been pulled or built locally.
        /// </summary>
        /// <param name="fromImage">Tag of the FROM image.</param>
        private string GetFromImageLocalTag(string fromImage) =>
            // Provides the overridable value of the registry (e.g. dotnetdocker.azurecr.io) because that is the registry that
            // would be used for tags that exist locally.
            GetFromImageTag(fromImage, Manifest.Registry);

        /// <summary>
        /// Gets the tag to use for the image of a FROM instruction.
        /// </summary>
        /// <param name="fromImage">Tag of the FROM image.</param>
        /// <param name="registry">Registry to use for comparing against the tag to determine if it's owned internally or external.</param>
        /// <remarks>
        /// This is meant to provide support for external images that need to be pulled from the mirror location.
        /// </remarks>
        private string GetFromImageTag(string fromImage, string? registry)
        {
            fromImage = Options.BaseImageOverrideOptions.ApplyBaseImageOverride(fromImage);

            if ((registry is not null && DockerHelper.IsInRegistry(fromImage, registry)) ||
                DockerHelper.IsInRegistry(fromImage, Manifest.Model.Registry)
                || Options.SourceRepoPrefix is null)
            {
                return fromImage;
            }

            string srcImage = TrimInternallyOwnedRegistryAndRepoPrefix(DockerHelper.NormalizeRepo(fromImage));
            return $"{Manifest.Registry}/{Options.SourceRepoPrefix}{srcImage}";
        }

        /// <summary>
        /// Returns the tag that represents the publicly available tag of a FROM instruction.
        /// </summary>
        /// <param name="fromImage">Tag of the FROM image.</param>
        /// <remarks>
        /// This compares the registry of the image tag to determine if it's internally owned. If so, it returns
        /// the tag using the raw (non-overriden) registry from the manifest (e.g. mcr.microsoft.com). Otherwise,
        /// it returns the image tag unchanged.
        /// </remarks>
        private string GetFromImagePublicTag(string fromImage)
        {
            string trimmed = TrimInternallyOwnedRegistryAndRepoPrefix(fromImage);
            if (trimmed == fromImage)
            {
                return Options.BaseImageOverrideOptions.ApplyBaseImageOverride(trimmed);
            }
            else
            {
                return $"{Manifest.Model.Registry}/{trimmed}";
            }
        }

        private string TrimInternallyOwnedRegistryAndRepoPrefix(string imageTag) =>
            IsInInternallyOwnedRegistry(imageTag) ?
                DockerHelper.TrimRegistry(imageTag).TrimStart(Options.RepoPrefix) :
                imageTag;

        private bool IsInInternallyOwnedRegistry(string imageTag) =>
            DockerHelper.IsInRegistry(imageTag, Manifest.Registry) ||
            DockerHelper.IsInRegistry(imageTag, Manifest.Model.Registry);

        private async Task PullBaseImagesAsync()
        {
            if (!Options.IsSkipPullingEnabled)
            {
                Logger.WriteHeading("PULLING LATEST BASE IMAGES");

                HashSet<string> pulledTags = new();
                HashSet<string> externalFromImages = new();

                foreach (PlatformInfo platform in Manifest.GetFilteredPlatforms())
                {
                    IEnumerable<string> platformExternalFromImages = platform.ExternalFromImages.Distinct();
                    externalFromImages.UnionWith(platformExternalFromImages);

                    foreach (string pullTag in platformExternalFromImages.Select(tag => GetFromImagePullTag(tag)))
                    {
                        if (!pulledTags.Contains(pullTag))
                        {
                            pulledTags.Add(pullTag);

                            // Pull the image, specifying its platform to ensure we get the necessary image in the case of a
                            // multi-arch tag.
                            _dockerService.PullImage(pullTag, platform.PlatformLabel, Options.IsDryRun);
                        }
                    }
                }

                if (pulledTags.Any())
                {
                    IEnumerable<string> finalStageExternalFromImages = Manifest.GetFilteredPlatforms()
                        .Where(platform => platform.FinalStageFromImage is not null && !platform.IsInternalFromImage(platform.FinalStageFromImage))
                        .Select(platform => GetFromImagePullTag(platform.FinalStageFromImage!))
                        .Distinct();

                    if (!finalStageExternalFromImages.IsSubsetOf(pulledTags))
                    {
                        throw new InvalidOperationException(
                            "The following tags are identified as final stage tags but were not pulled:" +
                            Environment.NewLine +
                            string.Join(", ", finalStageExternalFromImages.Except(pulledTags).ToArray()));
                    }

                    await Parallel.ForEachAsync(finalStageExternalFromImages, async (fromImage, cancellationToken) =>
                    {
                        // Ensure the digest of the pulled image is retrieved right away after pulling so it's available in
                        // the DockerServiceCache for later use.  The longer we wait to get the digest after pulling, the
                        // greater chance the tag could be updated resulting in a different digest returned than what was
                        // originally pulled.
                        await _imageDigestCache.GetImageDigestAsync(fromImage, Options.CredentialsOptions, Options.IsDryRun);
                    });

                    // Tag the images that were pulled from the mirror as they are referenced in the Dockerfiles
                    Parallel.ForEach(externalFromImages, fromImage =>
                    {
                        string pullTag = GetFromImagePullTag(fromImage);
                        if (pullTag != fromImage)
                        {
                            _dockerService.CreateTag(pullTag, fromImage, Options.IsDryRun);
                        }
                    });
                }
                else
                {
                    Logger.WriteMessage("No external base images to pull");
                }
            }
        }

        private IEnumerable<PlatformData> GetProcessedPlatforms() => _imageArtifactDetails?.Repos
            .Where(repoData => repoData.Images != null)
            .SelectMany(repoData => repoData.Images)
            .SelectMany(imageData => imageData.Platforms)
            ?? Enumerable.Empty<PlatformData>();

        private void PushImages()
        {
            if (Options.IsPushEnabled)
            {
                _loggerService.WriteHeading("PUSHING BUILT IMAGES");

                foreach (TagInfo tag in GetPushTags(_processedTags))
                {
                    _dockerService.PushImage(tag.FullyQualifiedName, Options.IsDryRun);
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
                    _loggerService.WriteMessage($"Replacing FROM `{fromImage}` with `{newFromImage}`");
                    Regex fromRegex = new Regex($@"FROM\s+{Regex.Escape(fromImage)}[^\s\r\n]*");
                    dockerfileContents = fromRegex.Replace(dockerfileContents, $"FROM {newFromImage}");
                    updateDockerfile = true;
                }

                if (updateDockerfile)
                {
                    // Don't overwrite the original dockerfile - write it to a new path.
                    dockerfilePath += ".temp";
                    _loggerService.WriteMessage($"Writing updated Dockerfile: {dockerfilePath}");
                    _loggerService.WriteMessage(dockerfileContents);
                    File.WriteAllText(dockerfilePath, dockerfileContents);
                }
            }

            return updateDockerfile;
        }

        private void WriteBuildSummary()
        {
            _loggerService.WriteHeading("IMAGES BUILT");

            if (_processedTags.Any())
            {
                foreach (TagInfo tag in _processedTags)
                {
                    _loggerService.WriteMessage(tag.FullyQualifiedName);
                }
            }
            else
            {
                _loggerService.WriteMessage("No images built");
            }

            _loggerService.WriteMessage();
        }

        private static string GetBuildCacheKey(PlatformInfo platform) =>
            $"{platform.DockerfilePathRelativeToManifest}-" +
            string.Join('-', platform.BuildArgs.Select(kvp => $"{kvp.Key}={kvp.Value}").ToArray());

        private class BuildCacheInfo
        {
            public BuildCacheInfo(string digest, string? baseImageDigest)
            {
                Digest = digest;
                BaseImageDigest = baseImageDigest;
            }

            public string Digest { get; }
            public string? BaseImageDigest { get; }
        }
    }
}
#nullable restore
