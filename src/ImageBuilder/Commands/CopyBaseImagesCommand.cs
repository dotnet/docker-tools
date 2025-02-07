// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Azure.ResourceManager.ContainerRegistry.Models;
using Microsoft.DotNet.DockerTools.ImageBuilder;
using Microsoft.DotNet.DockerTools.ImageBuilder.ViewModel;


#nullable enable
namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyBaseImagesCommand : CopyImagesCommand<CopyBaseImagesOptions, CopyBaseImagesOptionsBuilder>
    {
        private readonly IGitService _gitService;

        [ImportingConstructor]
        public CopyBaseImagesCommand(
            ICopyImageService copyImageService, ILoggerService loggerService, IGitService gitService)
            : base(copyImageService, loggerService)
        {
            _gitService = gitService;
        }

        protected override string Description => "Copies external base images from their source registry to ACR";

        public override void LoadManifest()
        {
            // This command can be used either locally from a product repo or from an external repo that references
            // a subscription file. If a subscription file is being used, don't attempt to load the manifest because
            // one may not exist (it wouldn't be used even if it did exist).
            if (Options.SubscriptionOptions.SubscriptionsPath is null)
            {
                base.LoadManifest();
            }
        }

        public override async Task ExecuteAsync()
        {
            LoggerService.WriteHeading("COPYING IMAGES");

            Options.BaseImageOverrideOptions.Validate();

            IEnumerable<ManifestInfo> manifests;
            string fullRegistryName;
            if (Options.SubscriptionOptions.SubscriptionsPath is null)
            {
                manifests = new ManifestInfo[] { Manifest };
                fullRegistryName = Manifest.Registry;
            }
            else
            {
                if (Options.RegistryOverride is null)
                {
                    throw new InvalidOperationException($"The '{ManifestOptions.RegistryOverrideName}' option must be set.");
                }

                manifests =
                    SubscriptionHelper.GetSubscriptionManifests(
                        Options.SubscriptionOptions.SubscriptionsPath, Options.FilterOptions, _gitService,
                        options => options.RegistryOverride = Options.RegistryOverride)
                    .Select(subscriptionManifest => subscriptionManifest.Manifest);
                fullRegistryName = Options.RegistryOverride;
            }

            IEnumerable<Task> copyImageTasks = manifests
                .SelectMany(manifest => GetFromImages(manifest))
                .Distinct()
                .Select(fromImage => CopyImageAsync(fromImage, fullRegistryName));

            await Task.WhenAll(copyImageTasks);
        }

        private IEnumerable<string> GetFromImages(ManifestInfo manifest) =>
            manifest.GetExternalFromImages()
                .Select(fromImage => Options.BaseImageOverrideOptions.ApplyBaseImageOverride(fromImage))
                .Where(fromImage => !fromImage.StartsWith(manifest.Model.Registry));

        private Task CopyImageAsync(string fromImage, string destinationRegistryName)
        {
            fromImage = DockerHelper.NormalizeRepo(fromImage);

            string registry = DockerHelper.GetRegistry(fromImage) ?? DockerHelper.DockerHubRegistry;
            string srcImage = DockerHelper.TrimRegistry(fromImage, registry);

            ContainerRegistryImportSourceCredentials? importSourceCreds = null;
            if (Options.CredentialsOptions.Credentials.TryGetValue(registry, out RegistryCredentials? registryCreds))
            {
                importSourceCreds = new ContainerRegistryImportSourceCredentials(registryCreds.Password)
                {
                    Username = registryCreds.Username
                };
            }

            return ImportImageAsync($"{Options.RepoPrefix}{fromImage}", destinationRegistryName, srcImage,
                srcRegistryName: registry, sourceCredentials: importSourceCreds);
        }
    }
}
#nullable disable
