// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class BuildCommand : ManifestCommand<BuildOptions, BuildOptionsBuilder>
    {
        private readonly IDockerService _dockerService;
        private readonly ILoggerService _loggerService;
        private readonly IGitService _gitService;
        private readonly IProcessService _processService;
        private readonly Lazy<ICopyImageService> _copyImageService;
        private readonly Lazy<IManifestService> _manifestService;
        private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
        private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
        private readonly IImageCacheService _imageCacheService;
        private readonly ImageDigestCache _imageDigestCache;
        private readonly List<TagInfo> _processedTags = new List<TagInfo>();
        private readonly HashSet<PlatformData> _builtPlatforms = new();
        private readonly Lazy<ImageNameResolverForBuild> _imageNameResolver;

        /// <summary>
        /// Maps a source digest from the image info file to the corresponding digest in the copied location for image caching.
        /// This is specifically needed to support shared Dockerfile scenarios.
        /// </summary>
        private readonly Dictionary<string, string> _sourceDigestCopyLocationMapping = new();

        private ImageArtifactDetails? _imageArtifactDetails;

        [ImportingConstructor]
        public BuildCommand(
            IDockerService dockerService,
            ILoggerService loggerService,
            IGitService gitService,
            IProcessService processService,
            ICopyImageServiceFactory copyImageServiceFactory,
            IManifestServiceFactory manifestServiceFactory,
            IRegistryCredentialsProvider registryCredentialsProvider,
            IAzureTokenCredentialProvider tokenCredentialProvider,
            IImageCacheService imageCacheService)
        {
            _dockerService = new DockerServiceCache(dockerService ?? throw new ArgumentNullException(nameof(dockerService)));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _processService = processService ?? throw new ArgumentNullException(nameof(processService));
            _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
            _tokenCredentialProvider = tokenCredentialProvider ?? throw new ArgumentNullException(nameof(tokenCredentialProvider));
            _imageCacheService = imageCacheService ?? throw new ArgumentNullException(nameof(imageCacheService));

            // Lazily create services which need access to options
            ArgumentNullException.ThrowIfNull(copyImageServiceFactory);
            _copyImageService = new Lazy<ICopyImageService>(() =>
                copyImageServiceFactory.Create(Options.AcrServiceConnection));
            ArgumentNullException.ThrowIfNull(manifestServiceFactory);
            _manifestService = new Lazy<IManifestService>(() =>
                manifestServiceFactory.Create(
                    ownedAcr: Options.RegistryOverride,
                    Options.AcrServiceConnection,
                    Options.CredentialsOptions));
            _imageDigestCache = new ImageDigestCache(_manifestService);

            _imageNameResolver = new Lazy<ImageNameResolverForBuild>(() =>
                new ImageNameResolverForBuild(
                    Options.BaseImageOverrideOptions,
                    Manifest,
                    Options.RepoPrefix,
                    Options.SourceRepoPrefix));
        }

        protected override string Description => "Builds Dockerfiles";

        public override async Task ExecuteAsync()
        {
            Options.BaseImageOverrideOptions.Validate();

            if (Options.ImageInfoOutputPath != null)
            {
                _imageArtifactDetails = new ImageArtifactDetails();
            }

            // Prepopulate the credential cache with the container registry scope so that the OIDC token isn't expired by the time we
            // need to query the registry at the end of the command.
            if (Options.IsPushEnabled)
            {
                _tokenCredentialProvider.GetCredential(
                    Options.AcrServiceConnection,
                    AzureScopes.ContainerRegistryScope);
            }

            await ExecuteWithDockerCredentialsAsync(PullBaseImagesAsync);
            await BuildImagesAsync();

            if (_processedTags.Count > 0 || _imageCacheService.HasAnyCachedPlatforms)
            {
                // Log in again to refresh token as it may have expired from a long build
                await ExecuteWithDockerCredentialsAsync(async () =>
                    {
                        PushImages();
                        await PublishImageInfoAsync();
                    });
            }

            WriteBuildSummary();
            WriteBuiltImagesToOutputVar();
        }

        private async Task ExecuteWithDockerCredentialsAsync(Func<Task> action)
        {
            await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
                isDryRun: Options.IsDryRun,
                action: action,
                credentialsOptions: Options.CredentialsOptions,
                registryName: Manifest.Registry,
                ownedAcr: Options.RegistryOverride,
                serviceConnection: Options.AcrServiceConnection);
        }

        private async Task ExecuteWithDockerCredentialsAsync(Action action)
        {
            await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
                isDryRun: Options.IsDryRun,
                action: action,
                credentialsOptions: Options.CredentialsOptions,
                registryName: Manifest.Registry,
                ownedAcr: Options.RegistryOverride,
                serviceConnection: Options.AcrServiceConnection);
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
                IEnumerable<TagInfo> pushTags = platform.PlatformInfo?.Tags ?? [];

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
                    (builtPlatform.PlatformInfo?.Tags ?? []).Any() &&
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
                    DockerHelper.GetRepo(_imageNameResolver.Value.GetFromImagePublicTag(platform.PlatformInfo.FinalStageFromImage)),
                    DockerHelper.GetDigestSha(baseImageDigest));
            }

            platform.BaseImageDigest = baseImageDigest;
        }

        private async Task SetPlatformDataLayersAsync(PlatformData platform, string tag)
        {
            if (platform.Layers == null || !platform.Layers.Any())
            {
                platform.Layers = (await _manifestService.Value.GetImageLayersAsync(tag, Options.IsDryRun)).ToList();
            }
        }

        private async Task SetPlatformDataDigestAsync(PlatformData platform, string tag)
        {
            // The digest of an image that is pushed to ACR is guaranteed to be the same when transferred to MCR.
            string? digest = await _imageDigestCache.GetLocalImageDigestAsync(tag, Options.IsDryRun);
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
                RepoData repoData = CreateRepoData(repoInfo);
                RepoData? srcRepoData = srcImageArtifactDetails?.Repos.FirstOrDefault(srcRepo => srcRepo.Repo == repoInfo.Name);

                foreach (ImageInfo image in repoInfo.FilteredImages)
                {
                    ImageData imageData = CreateImageData(image);
                    repoData.Images.Add(imageData);

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

                        PlatformData platformData = CreatePlatformData(image, platform);
                        imageData.Platforms.Add(platformData);

                        bool isCachedImage = false;
                        if (!Options.NoCache)
                        {
                            ImageCacheResult cacheResult = await _imageCacheService.CheckForCachedImageAsync(
                                srcImageData,
                                platformData,
                                _imageDigestCache,
                                _imageNameResolver.Value,
                                sourceRepoUrl: Options.SourceRepoUrl,
                                isLocalBaseImageExpected: true,
                                isDryRun: Options.IsDryRun);

                            if (cacheResult.State.HasFlag(ImageCacheState.Cached))
                            {
                                isCachedImage = true;

                                CopyPlatformDataFromCachedPlatform(platformData, cacheResult.Platform!);
                                platformData.IsUnchanged = cacheResult.State != ImageCacheState.CachedWithMissingTags;

                                await OnCacheHitAsync(repoInfo, allTagInfos, pullImage: cacheResult.IsNewCacheHit, cacheResult.Platform!.Digest);
                            }
                        }

                        if (!isCachedImage)
                        {
                            _processedTags.AddRange(allTagInfos);

                            BuildImage(platform, allTags);
                            _builtPlatforms.Add(platformData);

                            if (Options.IsPushEnabled && platform.FinalStageFromImage is not null)
                            {
                                platformData.BaseImageDigest =
                                   await _imageDigestCache.GetLocalImageDigestAsync(
                                       _imageNameResolver.Value.GetFromImageLocalTag(platform.FinalStageFromImage), Options.IsDryRun);
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

        private void CopyPlatformDataFromCachedPlatform(PlatformData dstPlatform, PlatformData srcPlatform)
        {
            // When a cache hit occurs for a Dockerfile, we want to transfer some of the metadata about the previously
            // published image so we don't need to recalculate it again.
            dstPlatform.BaseImageDigest = srcPlatform.BaseImageDigest;
            dstPlatform.Layers = new List<Layer>(srcPlatform.Layers);
        }

        private RepoData CreateRepoData(RepoInfo repoInfo) =>
            new RepoData
            {
                Repo = repoInfo.Name
            };

        private PlatformData CreatePlatformData(ImageInfo image, PlatformInfo platform)
        {
            PlatformData platformData = PlatformData.FromPlatformInfo(platform, image);
            platformData.SimpleTags = platform.Tags
                .Select(tag => tag.Name)
                .OrderBy(name => name)
                .ToList();

            return platformData;
        }

        private ImageData CreateImageData(ImageInfo image)
        {
            ImageData imageData =
                new ImageData
                {
                    ProductVersion = image.ProductVersion
                };

            if (image.SharedTags.Any())
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

            string baseImageTag = _imageNameResolver.Value.GetFromImageLocalTag(platform.FinalStageFromImage);

            // Base image should already be pulled or built so it's ok to inspect it
            (Models.Manifest.Architecture baseImageArch, string? baseImageVariant) =
                _dockerService.GetImageArch(baseImageTag, Options.IsDryRun);

            // Containerd normalizes arm64/v8 to arm64 with no variant.
            // In other words, arm64/v8 and arm64/ are compatible.
            // We still want to check variants if either variant is not "v8" or empty.
            // See https://github.com/moby/buildkit/issues/4039
            bool skipVariantCheck = platform.Model.Architecture == Models.Manifest.Architecture.ARM64
                && baseImageArch == Models.Manifest.Architecture.ARM64
                && ((platform.Model.Variant == "v8" || string.IsNullOrEmpty(platform.Model.Variant))
                    && (baseImageVariant == "v8" || string.IsNullOrEmpty(baseImageVariant)));

            if (platform.Model.Architecture != baseImageArch || (!skipVariantCheck && platform.Model.Variant != baseImageVariant))
            {
                throw new InvalidOperationException(
                    $"Platform '{platform.DockerfilePathRelativeToManifest}' is configured with an architecture that is not compatible with " +
                    $"the base image '{baseImageTag}':" + Environment.NewLine +
                    "Manifest platform:" + Environment.NewLine +
                        $"\tArchitecture: {platform.Model.Architecture}" + Environment.NewLine +
                        $"\tVariant: {platform.Model.Variant}" + Environment.NewLine +
                    "Base image:" + Environment.NewLine +
                        $"\tArchitecture: {baseImageArch}" + Environment.NewLine +
                        $"\tVariant: {baseImageVariant}");
            }
        }

        private void BuildImage(PlatformInfo platform, IEnumerable<string> allTags)
        {
            ValidatePlatformIsCompatibleWithBaseImage(platform);

            bool createdPrivateDockerfile = UpdateDockerfileFromCommands(platform, out string dockerfilePath);

            try
            {
                string? buildOutput = _dockerService.BuildImage(
                    dockerfilePath,
                    platform.BuildContextPath,
                    platform.PlatformLabel,
                    allTags,
                    GetBuildArgs(platform),
                    Options.IsRetryEnabled,
                    Options.IsDryRun);

                // Print image size
                string? firstTag = allTags.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));
                if (firstTag is not null)
                {
                    long size = _dockerService.GetImageSize(firstTag, Options.IsDryRun);
                    _loggerService.WriteMessage($"Image size (on disk): {size} bytes");
                }

                if (!Options.IsSkipPullingEnabled && !Options.IsDryRun && buildOutput?.Contains("Pulling from") == true)
                {
                    throw new InvalidOperationException(
                        "Build resulted in a base image being pulled. All image pulls should be done as a pre-build step. " +
                        "Any other image that's not accounted for means there's some kind of mistake in how things are " +
                        "configured or a bug in the code.");
                }
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
                await ExecuteWithDockerCredentialsAsync(() =>
                    {
                        // Don't need to provide the platform because we're pulling by digest. No need to worry about multi-arch tags.
                        _dockerService.PullImage(copiedSourceDigest, null, Options.IsDryRun);
                        _sourceDigestCopyLocationMapping[sourceDigest] = copiedSourceDigest;
                    });
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

            string[] destTags = allTags
                                .Select(tagInfo => DockerHelper.TrimRegistry(tagInfo.FullyQualifiedName))
                                .ToArray();
            string? srcRegistry = DockerHelper.GetRegistry(sourceDigest);
            await _copyImageService.Value.ImportImageAsync(
                Options.Subscription,
                Options.ResourceGroup,
                destTags,
                Manifest.Registry,
                DockerHelper.TrimRegistry(sourceDigest, srcRegistry),
                srcRegistry);

            // Redefine the source digest to be from the destination of the copy, not the source. The canonical scenario
            // here is to copy the cached image from MCR to the staging location in an ACR. This allows test jobs to always pull
            // from that staging location, not knowing whether it ended up there as a built image or a cached image.
            string destRepo = DockerHelper.GetRepo(DockerHelper.GetRepo(destTags.First()));
            sourceDigest = DockerHelper.GetImageName(Manifest.Registry, destRepo, digest: DockerHelper.GetDigestSha(sourceDigest));
            return sourceDigest;
        }

        private async Task PullBaseImagesAsync()
        {
            Logger.WriteHeading("PULLING LATEST BASE IMAGES");

            if (Options.IsSkipPullingEnabled)
            {
                Logger.WriteMessage("No external base images to pull");
                return;
            }

            HashSet<string> pulledTags = [];
            HashSet<string> externalFromImages = [];
            foreach (PlatformInfo platform in Manifest.GetFilteredPlatforms())
            {
                IEnumerable<string> platformExternalFromImages = platform.ExternalFromImages.Distinct();
                externalFromImages.UnionWith(platformExternalFromImages);

                IEnumerable<string> tagsToPull =
                    platformExternalFromImages.Select(_imageNameResolver.Value.GetFromImagePullTag);
                foreach (string pullTag in tagsToPull)
                {
                    if (pulledTags.Add(pullTag))
                    {
                        // Pull the image, specifying its platform to ensure we get the necessary image in the case of
                        // a multi-arch tag.
                        _dockerService.PullImage(pullTag, platform.PlatformLabel, Options.IsDryRun);
                    }
                }
            }

            if (pulledTags.Count <= 0)
            {
                Logger.WriteMessage("No external base images to pull");
                return;
            }

            IEnumerable<string> finalStageExternalFromImages =
                Manifest.GetFilteredPlatforms()
                    .Where(platform =>
                        platform.FinalStageFromImage is not null &&
                        !platform.IsInternalFromImage(platform.FinalStageFromImage))
                    .Select(platform =>
                        _imageNameResolver.Value.GetFromImagePullTag(platform.FinalStageFromImage!))
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
                await _imageDigestCache.GetLocalImageDigestAsync(fromImage, Options.IsDryRun);
            });

            // Tag the images that were pulled from the mirror as they are referenced in the Dockerfiles
            Parallel.ForEach(externalFromImages, fromImage =>
            {
                string pullTag = _imageNameResolver.Value.GetFromImagePullTag(fromImage);
                if (pullTag != fromImage)
                {
                    _dockerService.CreateTag(pullTag, fromImage, Options.IsDryRun);
                }
            });
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

                foreach (TagInfo tag in _processedTags)
                {
                    _dockerService.PushImage(tag.FullyQualifiedName, Options.IsDryRun);
                }
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
    }
}
