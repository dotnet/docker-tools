// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions>
    {
        private readonly IBuildPlanner _buildPlanner;
        private readonly ImageDigestCache _imageDigestCache;
        private readonly IManifestJsonService _manifestJsonService;
        private readonly ILogger<GetStaleImagesCommand> _logger;
        private readonly IOctokitClientFactory _octokitClientFactory;
        private readonly IGitService _gitService;

        public GetStaleImagesCommand(
            IManifestServiceFactory manifestServiceFactory,
            IManifestJsonService manifestJsonService,
            ILogger<GetStaleImagesCommand> logger,
            IOctokitClientFactory octokitClientFactory,
            IGitService gitService,
            IBuildPlanner buildPlanner)
        {
            _manifestJsonService = manifestJsonService ?? throw new ArgumentNullException(nameof(manifestJsonService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _octokitClientFactory = octokitClientFactory ?? throw new ArgumentNullException(nameof(octokitClientFactory));
            _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
            _buildPlanner = buildPlanner ?? throw new ArgumentNullException(nameof(buildPlanner));

            ArgumentNullException.ThrowIfNull(manifestServiceFactory);
            _imageDigestCache = new ImageDigestCache(
                new Lazy<IManifestService>(
                    () => manifestServiceFactory.Create(Options.CredentialsOptions)));
        }

        protected override string Description => "Gets paths to images whose base images are out-of-date";

        public override async Task ExecuteAsync()
        {
            if (Options.SubscriptionOptions.SubscriptionsPath is null)
            {
                throw new InvalidOperationException("Subscriptions path must be set.");
            }

            IEnumerable<Task<SubscriptionImagePaths>> getPathResults =
                SubscriptionHelper.GetSubscriptionManifests(
                    Options.SubscriptionOptions.SubscriptionsPath,
                    Options.FilterOptions,
                    _gitService,
                    _manifestJsonService,
                    manifestOptions => manifestOptions.RegistryOverride = Options.RegistryOverride)
                .Select(async subscriptionManifest =>
                    new SubscriptionImagePaths
                    {
                        SubscriptionId = subscriptionManifest.Subscription.Id,
                        ImagePaths =
                            (await GetPathsToRebuildAsync(subscriptionManifest.Subscription, subscriptionManifest.Manifest))
                            .ToArray()
                    });

            SubscriptionImagePaths[] results = await Task.WhenAll(getPathResults);

            // Filter out any results that don't have any images to rebuild
            results = results
                .Where(result => result.ImagePaths.Any())
                .ToArray();

            string outputString = JsonConvert.SerializeObject(results);

            _logger.LogInformation(
                PipelineHelper.FormatOutputVariable(Options.VariableName, outputString)
                    .Replace("\"", "\\\"")); // Escape all quotes

            string formattedResults = JsonConvert.SerializeObject(results, Formatting.Indented);
            _logger.LogInformation(
                $"Image Paths to be Rebuilt:{Environment.NewLine}{formattedResults}");
        }

        private async Task<IEnumerable<string>> GetPathsToRebuildAsync(Models.Subscription.Subscription subscription, ManifestInfo manifest)
        {
            ImageArtifactDetails imageArtifactDetails = await GetImageInfoForSubscriptionAsync(subscription, manifest);

            return await StaleImageHelper.GetStaleDockerfilePathsAsync(
                _buildPlanner,
                manifest,
                imageArtifactDetails,
                _imageDigestCache,
                Options.BaseImageOverrideOptions,
                Options.SourceRepoPrefix,
                Options.IsDryRun);
        }

        private async Task<ImageArtifactDetails> GetImageInfoForSubscriptionAsync(Models.Subscription.Subscription subscription, ManifestInfo manifest)
        {
            ITreesClient treesClient = await _octokitClientFactory.CreateTreesClientAsync(Options.GitOptions.GitHubAuthOptions);
            string fileSha = await treesClient.GetFileShaAsync(
                subscription.ImageInfo.Owner, subscription.ImageInfo.Repo, subscription.ImageInfo.Branch, subscription.ImageInfo.Path);

            IBlobsClient blobsClient = await _octokitClientFactory.CreateBlobsClientAsync(Options.GitOptions.GitHubAuthOptions);
            string imageDataJson = await blobsClient.GetFileContentAsync(subscription.ImageInfo.Owner, subscription.ImageInfo.Repo, fileSha);

            return ImageInfoHelper.LoadFromContent(imageDataJson, manifest, skipManifestValidation: true);
        }
    }
}
