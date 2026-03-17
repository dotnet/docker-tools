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
public class CreateManifestListCommand : ManifestCommand<CreateManifestListOptions, CreateManifestListOptionsBuilder>
{
    private readonly Lazy<IManifestService> _manifestService;
    private readonly IDockerService _dockerService;
    private readonly ILogger<CreateManifestListCommand> _logger;
    private readonly IDateTimeService _dateTimeService;
    private readonly IRegistryCredentialsProvider _registryCredentialsProvider;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider;

    public CreateManifestListCommand(
        IManifestJsonService manifestJsonService,
        IManifestServiceFactory manifestServiceFactory,
        IDockerService dockerService,
        ILogger<CreateManifestListCommand> logger,
        IDateTimeService dateTimeService,
        IRegistryCredentialsProvider registryCredentialsProvider,
        IAzureTokenCredentialProvider tokenCredentialProvider) : base(manifestJsonService)
    {
        _dockerService = dockerService ?? throw new ArgumentNullException(nameof(dockerService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dateTimeService = dateTimeService ?? throw new ArgumentNullException(nameof(dateTimeService));
        _registryCredentialsProvider = registryCredentialsProvider ?? throw new ArgumentNullException(nameof(registryCredentialsProvider));
        _tokenCredentialProvider = tokenCredentialProvider ?? throw new ArgumentNullException(nameof(tokenCredentialProvider));

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

        ImageArtifactDetails imageArtifactDetails = ImageInfoHelper.LoadFromFile(Options.ImageInfoPath, Manifest);

        await _registryCredentialsProvider.ExecuteWithCredentialsAsync(
            Options.IsDryRun,
            async () =>
            {
                IReadOnlyList<ManifestListInfo> manifestLists =
                    ManifestListHelper.GetManifestListsForChangedImages(
                        Manifest, imageArtifactDetails, Options.RepoPrefix);

                foreach (ManifestListInfo manifestListInfo in manifestLists)
                {
                    _dockerService.CreateManifestList(manifestListInfo.Tag, manifestListInfo.PlatformTags, Options.IsDryRun);
                }

                DateTime createdDate = _dateTimeService.UtcNow;

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

    private async Task SaveTagInfoToImageInfoFileAsync(DateTime createdDate, ImageArtifactDetails imageArtifactDetails)
    {
        _logger.LogInformation("SETTING TAG INFO");

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

    private void WriteManifestSummary(IReadOnlyList<ManifestListInfo> manifestLists)
    {
        foreach (ManifestListInfo manifestListInfo in manifestLists)
            _logger.LogInformation(manifestListInfo.Tag);

        if (manifestLists.Count == 0)
            _logger.LogInformation("No manifest lists created");

        _logger.LogInformation(string.Empty);
    }
}
