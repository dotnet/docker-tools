// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.DotNet.ImageBuilder.Models.Subscription;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.DotNet.ImageBuilder.ViewModel;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class CopyBaseImagesCommand : CopyImagesCommand<CopyBaseImagesOptions, CopyBaseImagesOptionsBuilder>
    {
        private readonly HttpClient _httpClient;

        [ImportingConstructor]
        public CopyBaseImagesCommand(
            IAzureManagementFactory azureManagementFactory, ILoggerService loggerService, IHttpClientProvider httpClientProvider)
            : base(azureManagementFactory, loggerService)
        {
            _httpClient = httpClientProvider.GetClient();
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

            IEnumerable<Task> copyImageTasks;
            if (Options.SubscriptionOptions.SubscriptionsPath is null)
            {
                copyImageTasks = GetFromImages(Manifest)
                    .Select(fromImage => CopyImageAsync(fromImage, GetBaseRegistryName(Manifest.Registry)));
            }
            else
            {
                if (Options.RegistryOverride is null)
                {
                    throw new InvalidOperationException($"{Options.RegistryOverride} must be set.");
                }

                IEnumerable<(Subscription Subscription, ManifestInfo Manifest)> subscriptionManifests =
                    await SubscriptionHelper.GetSubscriptionManifestsAsync(
                        Options.SubscriptionOptions.SubscriptionsPath, Options.FilterOptions, _httpClient,
                        options => options.RegistryOverride = Options.RegistryOverride);
                copyImageTasks = subscriptionManifests
                    .SelectMany(subscriptionManifest => GetFromImages(subscriptionManifest.Manifest))
                    .Distinct()
                    .Select(fromImage => CopyImageAsync(fromImage, GetBaseRegistryName(Options.RegistryOverride)));
            }

            await Task.WhenAll(copyImageTasks);
        }

        private static IEnumerable<string> GetFromImages(ManifestInfo manifest) =>
            manifest.GetExternalFromImages()
                .Where(fromImage => !fromImage.StartsWith(manifest.Model.Registry));

        private Task CopyImageAsync(string fromImage, string destinationRegistryName)
        {
            string registry = DockerHelper.GetRegistry(fromImage) ?? "docker.io";
            fromImage = DockerHelper.TrimRegistry(fromImage, registry);

            ImportSourceCredentials? importSourceCreds = null;
            if (Options.CredentialsOptions.Credentials.TryGetValue(registry, out RegistryCredentials? registryCreds))
            {
                importSourceCreds = new ImportSourceCredentials(registryCreds.Password, registryCreds.Username);
            }

            return ImportImageAsync($"{Options.RepoPrefix}{fromImage}", destinationRegistryName, fromImage,
                srcRegistryName: registry, sourceCredentials: importSourceCreds);
        }
    }
}
#nullable disable
