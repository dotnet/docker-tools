// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class PublishManifestCommand : ManifestCommand<PublishManifestOptions, PublishManifestOptionsBuilder>
    {
        private readonly Lazy<IManifestService> _manifestService;
        private readonly IDockerService _dockerService;
        private readonly ILoggerService _loggerService;
        private readonly IDateTimeService _dateTimeService;
        private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
        private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;
        private ConcurrentBag<string> _publishedManifestTags = new();

        public PublishManifestCommand(
            IManifestServiceFactory manifestServiceFactory,
            IDockerService dockerService,
            ILoggerService loggerService,
            IDateTimeService dateTimeService,
            IRegistryCredentialsProvider registryCredentialsProvider,
            IAzureTokenCredentialProvider tokenCredentialProvider)
        {
            _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));
            _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
            _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
            _tokenCredentialProvider = tokenCredentialProvider ?? throw new ArgumentNullException(nameof(tokenCredentialProvider));

            // Lazily create the Manifest Service so it can have access to Options (not available in this constructor)
            ArgumentNullException.ThrowIfNull(manifestServiceFactory);
            _manifestService = new Lazy<IManifestService>(() =>
                manifestServiceFactory.Create(
                    ownedAcr: Options.RegistryOverride,
                    Options.AcrServiceConnection,
                    Options.CredentialsOptions));
        }

        protected override string Description => "Creates and publishes the manifest to the Docker Registry";

        public override async Task ExecuteAsync()
        {
            _loggerService.WriteHeading("GENERATING MANIFESTS");

            if (!File.Exists(Options.ImageInfoPath))
            {
                _loggerService.WriteMessage(PipelineHelper.FormatWarningCommand(
                    "Image info file not found. Skipping manifest publishing."));
                return;
            }

            // Prepopulate the credential cache with the container registry scope so that the OIDC token isn't expired by the time we
            // need to query the registry at the end of the command.
            if (!Options.IsDryRun)
            {
                _tokenCredentialProvider.GetCredential(
                    Options.AcrServiceConnection,
                    AzureScopes.ContainerRegistryScope);
            }

            ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

            await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
                Options.IsDryRun,
                async () =>
                {
                    IEnumerable<(RepoInfo repo, ImageInfo image)> manifests = Manifest.FilteredRepos
                        .SelectMany(repo =>
                            repo.FilteredImages
                                .Where(image => image.SharedTags.Any())
                                .Where(image => image.AllPlatforms
                                    .Select(platform =>
                                        ImageInfoHelper.GetMatchingPlatformData(platform, repo, imageArtifactDetails))
                                    .Where(platformMapping => platformMapping != null)
                                    .Any(platformMapping => !platformMapping?.Platform.IsUnchanged ?? false))
                                .Select(image => (repo, image)))
                        .ToList();

                    Parallel.ForEach(manifests, ((RepoInfo Repo, ImageInfo Image) repoImage) =>
                    {
                        GenerateManifests(repoImage.Repo, repoImage.Image);
                    });

                    DateTime createdDate = _dateTimeService.UtcNow;
                    Parallel.ForEach(_publishedManifestTags, tag =>
                    {
                        _dockerService.PushManifestList(tag, Options.IsDryRun);
                    });

                    WriteManifestSummary();

                    await SaveTagInfoToImageInfoFileAsync(createdDate, imageArtifactDetails);
                },
                Options.CredentialsOptions,
                registryName: Manifest.Registry,
                ownedAcr: Manifest.Registry,
                serviceConnection: Options.AcrServiceConnection);
        }

        private async Task SaveTagInfoToImageInfoFileAsync(DateTime createdDate, ImageArtifactDetails imageArtifactDetails)
        {
            _loggerService.WriteSubheading("SETTING TAG INFO");

            IEnumerable<ImageData> images = imageArtifactDetails.Repos
                .SelectMany(repo => repo.Images)
                .Where(image => image.Manifest != null);

            foreach (ImageData image in images)
            {
                image.Manifest.Created = createdDate;

                TagInfo sharedTag = image.ManifestImage.SharedTags.First();

                image.Manifest.Digest = DockerHelper.GetDigestString(
                    image.ManifestRepo.FullModelName,
                    await _manifestService.Value.GetManifestDigestShaAsync(
                        sharedTag.FullyQualifiedName, Options.IsDryRun));

                IEnumerable<(string Repo, string Tag)> syndicatedRepresentativeSharedTags = image.ManifestImage.SharedTags
                    .Where(tag => tag.SyndicatedRepo is not null)
                    .GroupBy(tag => tag.SyndicatedRepo)
                    .Select(group => (group.Key, group.First().SyndicatedDestinationTags.First()))
                    .Cast<(string Repo, string Tag)>()
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

        private void GenerateManifests(RepoInfo repo, ImageInfo image)
        {
            GenerateManifests(repo, image, image.SharedTags.Select(tag => tag.Name),
                tag => DockerHelper.GetImageName(Manifest.Registry, Options.RepoPrefix + repo.Name, tag),
                platform => platform.Tags.First());

            IEnumerable<IGrouping<string, TagInfo>> syndicatedTagGroups = image.SharedTags
                .Where(tag => tag.SyndicatedRepo != null)
                .GroupBy(tag => tag.SyndicatedRepo);

            foreach (IGrouping<string, TagInfo> syndicatedTags in syndicatedTagGroups)
            {
                string syndicatedRepo = syndicatedTags.Key;
                IEnumerable<string> destinationTags = syndicatedTags.SelectMany(tag => tag.SyndicatedDestinationTags);

                // There won't always be a platform tag that's syndicated. So if a manifest tag is syndicated, we need to account
                // for the possibility that a given platform for that manifest will not have a matching syndicated repo.

                GenerateManifests(repo, image, destinationTags,
                    tag => DockerHelper.GetImageName(Manifest.Registry, Options.RepoPrefix + syndicatedRepo, tag),
                    platform => platform.Tags.FirstOrDefault(tag => tag.SyndicatedRepo == syndicatedRepo));
            }
        }

        private void GenerateManifests(RepoInfo repo, ImageInfo image, IEnumerable<string> tags, Func<string, string> getImageName,
            Func<PlatformInfo, TagInfo?> getTagRepresentative)
        {
            foreach (string tag in tags)
            {
                GenerateManifest(repo, image, tag, getImageName, getTagRepresentative);
            }
        }

        private void GenerateManifest(RepoInfo repo, ImageInfo image, string tag, Func<string, string> getImageName,
            Func<PlatformInfo, TagInfo?> getTagRepresentative)
        {
            string manifestListTag = getImageName(tag);
            _publishedManifestTags.Add(manifestListTag);

            List<string> images = new();

            foreach (PlatformInfo platform in image.AllPlatforms)
            {
                TagInfo? imageTag;
                if (platform.Tags.Any())
                {
                    imageTag = getTagRepresentative(platform);
                }
                else
                {
                    PlatformInfo platformInfo = repo.AllImages
                        .SelectMany(image =>
                            image.AllPlatforms
                                .Select(p => (Image: image, Platform: p))
                                .Where(imagePlatform => platform != imagePlatform.Platform &&
                                    PlatformInfo.AreMatchingPlatforms(image, platform, imagePlatform.Image, imagePlatform.Platform) &&
                                    imagePlatform.Platform.Tags.Any()))
                        .FirstOrDefault()
                        .Platform;

                    if (platformInfo is null)
                    {
                        throw new InvalidOperationException(
                            $"Could not find a platform with concrete tags for '{platform.DockerfilePathRelativeToManifest}'.");
                    }

                    imageTag = getTagRepresentative(platformInfo);
                }

                if (imageTag is not null)
                {
                    images.Add(getImageName(imageTag.Name));
                }
            }

            _dockerService.CreateManifestList(manifestListTag, images, Options.IsDryRun);
        }

        private void WriteManifestSummary()
        {
            _loggerService.WriteHeading("MANIFEST TAGS PUBLISHED");

            if (_publishedManifestTags.Any())
            {
                foreach (string tag in _publishedManifestTags)
                {
                    _loggerService.WriteMessage(tag);
                }
            }
            else
            {
                _loggerService.WriteMessage("No manifests published");
            }

            _loggerService.WriteMessage();
        }
    }
}
