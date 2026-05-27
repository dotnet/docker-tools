// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands;

/// <summary>
/// Creates Docker manifest lists and records their digests in the image info file.
/// Designed to run in the Post_Build stage after image info fragments have been merged.
/// </summary>
public class CreateManifestListCommand : ManifestCommand<CreateManifestListOptions>
{
    private readonly Lazy<IManifestService> _manifestService;
    private readonly IDockerService _dockerService;
    private readonly ICopyImageService _copyImageService;
    private readonly ILogger<CreateManifestListCommand> _logger;
    private readonly IDateTimeService _dateTimeService;
    private readonly IRegistryCredentialsProvider _registryCredentialsProvider;

    public CreateManifestListCommand(
        IManifestJsonService manifestJsonService,
        IManifestServiceFactory manifestServiceFactory,
        IDockerService dockerService,
        ICopyImageService copyImageService,
        ILogger<CreateManifestListCommand> logger,
        IDateTimeService dateTimeService,
        IRegistryCredentialsProvider registryCredentialsProvider) : base(manifestJsonService)
    {
        _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
        _copyImageService = copyImageService ?? throw new ArgumentNullException(nameof(copyImageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
        _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));

        ArgumentNullException.ThrowIfNull(manifestServiceFactory);
        _manifestService = new Lazy<IManifestService>(() =>
            manifestServiceFactory.Create(Options.CredentialsOptions));
    }

    protected override string Description => "Creates manifest lists and records their digests in image info";

    public override async Task ExecuteAsync()
    {
        _logger.LogInformation("CREATING MANIFEST LISTS");

        if (!File.Exists(Options.ImageInfoPath))
        {
            _logger.LogInformation(PipelineHelper.FormatWarningCommand(
                "Image info file not found. Skipping manifest list creation."));
            return;
        }

        // The merged image-info file is the source of truth for which images were
        // built in this run and therefore which shared-tag manifest lists need updates.
        ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

        await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
            Options.IsDryRun,
            async () =>
            {
                // Path-filtered builds may include only one platform of a shared-tag image. Import
                // any missing sibling platforms into staging first, then add them to image-info so
                // manifest-list creation sees the complete set.
                _logger.LogInformation("Looking for platforms missing from the current build.");
                IReadOnlyList<PlatformImportData> platformsToImport =
                    await GetMissingPlatformsAsync(imageArtifactDetails);
                _logger.LogInformation(
                    "Found {NumberOfPlatformsToImport} platforms to import.",
                    platformsToImport.Count);

                foreach (PlatformImportData platformToImport in platformsToImport)
                {
                    _logger.LogDebug(
                        "Importing platform {Platform} for multi-platform image {SharedTags}.",
                        platformToImport.Platform.PlatformLabel,
                        $"[{string.Join(',', platformToImport.Image.SharedTags)}]");

                    await ImportPlatformToStagingAsync(platformToImport);

                    // Add the imported platform to the image-info so that it's present for
                    // manifest list creation.
                    AddPlatformToImageInfo(platformToImport);
                }

                // Build the manifest-list definitions from the now-complete image-info.
                IReadOnlyList<ManifestListInfo> manifestLists =
                    ManifestListHelper.GetManifestListsForImages(Manifest, imageArtifactDetails, Options.RepoPrefix);

                // Create each local manifest list tag from its platform-specific tags.
                // This both creates and pushes the new manifest lists.
                foreach (ManifestListInfo manifestListInfo in manifestLists)
                {
                    _dockerService.CreateManifestList(
                        manifestListTag: manifestListInfo.Tag,
                        images: manifestListInfo.PlatformTags,
                        isDryRun: Options.IsDryRun);
                }

                DateTime createdDate = _dateTimeService.UtcNow;

                // Push the manifest lists, then record their digests back into image-info.
                Parallel.ForEach(manifestLists, manifestListInfo =>
                {
                    _dockerService.PushManifestList(manifestListInfo.Tag, Options.IsDryRun);
                });

                WriteManifestSummary(manifestLists);

                await SaveTagInfoToImageInfoFileAsync(createdDate, imageArtifactDetails);
            },
            Options.CredentialsOptions,
            registryName: Manifest.Registry);
    }

    /// <summary>
    /// Path-filtered builds produce an image-info containing only the rebuilt platforms. If we
    /// don't copy over the other platforms that were not built as part of this job, then the
    /// shared-tag manifest list created from that image-info would only reference a subset of its
    /// platforms. This method finds the missing platforms that need to be imported, and returns
    /// instructions for how to import them (i.e. where to import from and what tags they need).
    /// </summary>
    private async Task<IReadOnlyList<PlatformImportData>> GetMissingPlatformsAsync(ImageArtifactDetails imageArtifactDetails)
    {
        if (Options.IsDryRun)
        {
            return [];
        }

        // Find every (imageData, platform) pair that the manifest declares for a shared-tag image
        // but that wasn't built this run. These are the platforms we need to port.
        IEnumerable<(ImageData ImageData, PlatformInfo Platform)> candidatePlatforms =
            imageArtifactDetails.Repos
            .SelectMany(repoData => repoData.Images)
            .Where(imageData =>
                // If imageData.Manifest (ManifestData) is not null,
                // then the image likely has some shared tags.
                imageData.Manifest is not null
                // ImageInfoHelper leaves these null for image-info rows that no longer map to the
                // current manifest, such as stale removed repos/images.
                && imageData.ManifestImage is not null
                && imageData.ManifestRepo is not null)
            .SelectMany(imageData =>
                {
                    // Cross from image-info world to manifest world via back-pointers, so that the
                    // set difference below operates on like-typed PlatformInfo values.
                    List<PlatformInfo> declaredPlatforms = [.. imageData.ManifestImage.AllPlatforms];
                    HashSet<PlatformInfo> builtPlatforms = imageData.Platforms
                        .Select(platformData => platformData.PlatformInfo)
                        // PlatformInfo back-pointer is nullable; an image-info row with no mapping
                        // doesn't correspond to a declared platform, so exclude it.
                        .OfType<PlatformInfo>()
                        .ToHashSet();

                    // A declared platform needs porting if no built platform corresponds to it.
                    List<PlatformInfo> missingPlatforms =
                        [.. declaredPlatforms.Where(platform => !builtPlatforms.Contains(platform))];

                    // Log all multi-platform tags, to avoid any ambiguity.
                    string sharedTagsString = $"[{string.Join(',', imageData.ManifestImage.SharedTags)}]";

                    _logger.LogInformation(
                        "Multi-platform image {SharedTags} built {BuiltPlatforms}/{TotalPlatforms} platforms (missing: {MissingPlatforms})",
                        sharedTagsString, builtPlatforms.Count, declaredPlatforms.Count, missingPlatforms.Count);

                    foreach (PlatformInfo platform in declaredPlatforms)
                    {
                        _logger.LogDebug(
                            "Multi-platform image {SharedTags} has platform {Platform}. WasBuilt={WasBuilt}",
                            sharedTagsString, platform.PlatformLabel, builtPlatforms.Contains(platform));
                    }

                    // Finally, project to a tuple.
                    return missingPlatforms
                        .Select(declaredPlatform => (ImageData: imageData, Platform: declaredPlatform));
                });

        List<PlatformImportData> platformsToImport = [];
        foreach ((ImageData imageData, PlatformInfo platform) in candidatePlatforms)
        {
            string sourceRepo = imageData.ManifestRepo.Name;
            string sourceTag = platform.GetRepresentativeTag().Name;

            // Look up the platform's currently-published digest.
            ImageName sourceName = new(Manifest.Model.Registry, sourceRepo, sourceTag, digest: null);
            ManifestQueryResult result = await _manifestService.Value.GetManifestAsync(sourceName, isDryRun: false);

            var platformImportData = new PlatformImportData(
                Repo: imageData.ManifestRepo,
                Image: imageData.ManifestImage,
                Platform: platform,
                SourceDigest: result.ContentDigest,
                DestinationTags: GetDestinationTagsForImage(platform, sourceRepo),
                SiblingPlatforms: imageData.Platforms);

            platformsToImport.Add(platformImportData);
        }

        return platformsToImport;
    }

    /// <summary>
    /// Imports a platform image into the scoped staging registry.
    /// </summary>
    private async Task ImportPlatformToStagingAsync(PlatformImportData platformToImport)
    {
        // Build the registry-less reference 'repo@sha256:...' that ImportImageAsync expects for
        // srcTagName. ImportImageAsync concatenates srcRegistryName and srcTagName itself (see
        // CopyImageService.ImportImageAsync), so the registry is passed separately via
        // srcRegistryName below and must not appear here.
        string srcReference =
            DockerHelper.GetImageName(
                registry: null,
                repo: platformToImport.Repo.Name,
                tag: null,
                digest: platformToImport.SourceDigest);

        await _copyImageService.ImportImageAsync(
            destTagNames: [.. platformToImport.DestinationTags],
            destAcrName: Manifest.Registry,
            srcTagName: srcReference,
            copyReferrers: true,
            srcRegistryName: Manifest.Model.Registry,
            isDryRun: Options.IsDryRun);
    }

    private IReadOnlyList<string> GetDestinationTagsForImage(PlatformInfo platform, string sourceRepo)
    {
        string prefix = Options.RepoPrefix ?? string.Empty;
        string destRepo = $"{prefix}{sourceRepo}";

        IEnumerable<string> primaryTags = platform.Tags
            .Select(tag => TagInfo.GetFullyQualifiedName(destRepo, tag.Name));

        // For syndicated platform tags, also import to the syndicated repo paths so syndicated
        // manifest lists can resolve their platform references.
        IEnumerable<string> syndicatedTags = platform.Tags
            .Where(tag => tag.SyndicatedRepo is not null)
            .SelectMany(tag => tag.SyndicatedDestinationTags
                .Select(destTag => TagInfo.GetFullyQualifiedName(
                    $"{prefix}{tag.SyndicatedRepo}", destTag)));

        return [.. primaryTags, .. syndicatedTags];
    }

    private static void AddPlatformToImageInfo(PlatformImportData importedPlatform)
    {
        PlatformData platformData = PlatformData.FromPlatformInfo(importedPlatform.Platform, importedPlatform.Image);
        platformData.Digest = DockerHelper.GetDigestString(importedPlatform.Repo.FullModelName, importedPlatform.SourceDigest);

        // Marking the platform as unchanged allows it to be trimmed by the trimUnchangedImages
        // command in the Publish stage, just like cache hits during the Build stage. This prevents
        // it from affecting build telemetry, image-info publishing, and and build notifications.
        //
        // The signing stage still sees these images, but we explicitly set `copyReferrers: true`
        // when importing, meaning the imported image brings along its existing signature and any
        // other referrers. The signImages command is idempotent - it won't double-sign an image.
        platformData.IsUnchanged = true;

        importedPlatform.SiblingPlatforms.Add(platformData);
    }

    private async Task SaveTagInfoToImageInfoFileAsync(DateTime createdDate, ImageArtifactDetails imageArtifactDetails)
    {
        _logger.LogInformation("SETTING TAG INFO");

        IEnumerable<ImageData> images = imageArtifactDetails.Repos
            .SelectMany(repo => repo.Images)
            .Where(image => image.Manifest != null);

        foreach (ImageData image in images)
        {
            if (image.ManifestImage is null || image.ManifestRepo is null)
            {
                continue;
            }

            image.Manifest.Created = createdDate;

            TagInfo sharedTag = image.ManifestImage.SharedTags.First();

            image.Manifest.Digest = DockerHelper.GetDigestString(
                image.ManifestRepo.FullModelName,
                await _manifestService.Value.GetManifestDigestShaAsync(
                    sharedTag.FullyQualifiedName, Options.IsDryRun));

            IEnumerable<(string Repo, string Tag)> syndicatedRepresentativeSharedTags = image.ManifestImage.SharedTags
                .Where(tag => tag.SyndicatedRepo is not null)
                .GroupBy(tag => tag.SyndicatedRepo)
                .Select(group => (Repo: group.Key, Tag: group.First().SyndicatedDestinationTags.First()))
                .OrderBy(obj => obj.Repo)
                .ThenBy(obj => obj.Tag);

            foreach ((string Repo, string Tag) syndicatedSharedTag in syndicatedRepresentativeSharedTags)
            {
                string digest = DockerHelper.GetDigestString(
                    DockerHelper.GetImageName(Manifest.Model.Registry, syndicatedSharedTag.Repo),
                    await _manifestService.Value.GetManifestDigestShaAsync(
                        DockerHelper.GetImageName(Manifest.Registry, Options.RepoPrefix + syndicatedSharedTag.Repo, syndicatedSharedTag.Tag),
                        Options.IsDryRun));
                image.Manifest.SyndicatedDigests.Add(digest);
            }
        }

        string imageInfoString = JsonHelper.SerializeObject(imageArtifactDetails);
        File.WriteAllText(Options.ImageInfoPath, imageInfoString);
    }

    private void WriteManifestSummary(IReadOnlyList<ManifestListInfo> manifestLists)
    {
        foreach (ManifestListInfo manifestListInfo in manifestLists)
            _logger.LogInformation(manifestListInfo.Tag);

        if (manifestLists.Count == 0)
            _logger.LogInformation("No manifest lists created");
    }

    /// <summary>
    /// A plan to re-import a previously-published platform-specific image into the staging
    /// registry.
    /// </summary>
    private sealed record PlatformImportData(
        RepoInfo Repo,
        ImageInfo Image,
        PlatformInfo Platform,
        string SourceDigest,
        IReadOnlyList<string> DestinationTags,
        ICollection<PlatformData> SiblingPlatforms);
}
