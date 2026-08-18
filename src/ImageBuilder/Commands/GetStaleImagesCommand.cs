// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Build;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class GetStaleImagesCommand : Command<GetStaleImagesOptions>
    {
        private readonly BuildPlanner _buildPlanner;
        private readonly ImageDigestCache _imageDigestCache;
        private readonly Lazy<IManifestService> _manifestService;
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
            BuildPlanner buildPlanner)
        {
            _manifestJsonService = manifestJsonService ?? throw new ArgumentNullException(nameof(manifestJsonService));
            _logger = logger;
            _octokitClientFactory = octokitClientFactory;
            _gitService = gitService;
            _buildPlanner = buildPlanner ?? throw new ArgumentNullException(nameof(buildPlanner));

            // Don't worry about authenticating to our own ACR, since we are checking base image digests from public
            // registries instead of our staging location. Registry credentials are needed however to prevent rate
            // limiting on other registries we don't own.
            ArgumentNullException.ThrowIfNull(manifestServiceFactory);
            _manifestService = new Lazy<IManifestService>(() =>
                manifestServiceFactory.Create(Options.CredentialsOptions));
            _imageDigestCache = new ImageDigestCache(_manifestService);
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

            ImageNameResolverForMatrix imageNameResolver = new(
                Options.BaseImageOverrideOptions,
                manifest,
                repoPrefix: null,
                sourceRepoPrefix: Options.SourceRepoPrefix);

            BuildGraph graph = BuildGraph.CreateFiltered(manifest);

            // This command only reports images made stale by missing metadata or base image updates.
            // Dockerfile and tag changes are handled by normal build planning.
            IBuildPolicy policy = new CompositeBuildPolicy(
                defaultResult: new BuildPolicyResult(
                    BuildAction.NoAction,
                    new BuildReason("All checks passed, so no work is required.")),
                logger: _logger,
                policies:
                [
                    // Rebuild when no published image metadata exists.
                    new MissingPublishedImagePolicy(),

                    // Rebuild when the registry digest for the base image has changed.
                    BaseImageChangedPolicy.FromRegistry(
                        _imageDigestCache,
                        imageNameResolver,
                        Options.IsDryRun)
                ]);

            BuildPlanItem[] plan = await _buildPlanner.CreatePlanAsync(graph, imageArtifactDetails, policy);

            return plan
                .Where(item => item.Decision.Action != BuildAction.NoAction)
                .Select(item => item.Target.Platform.Model.Dockerfile)
                .Distinct();
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
