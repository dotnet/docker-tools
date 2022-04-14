// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Rest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IManifestService))]
    public class ManifestService : IManifestService
    {
        private readonly RegistryHttpClient _httpClient;

        [ImportingConstructor]
        public ManifestService(IHttpClientProvider httpClientProvider)
        {
            _httpClient = httpClientProvider.GetRegistryClient();
        }

        public void PushFromSpec(string manifestFile, bool isDryRun)
        {
            // ExecuteWithRetry because the manifest-tool fails periodically while communicating
            // with the Docker Registry.
            ExecuteHelper.ExecuteWithRetry("manifest-tool", $"push from-spec {manifestFile}", isDryRun);
        }

        public Task<ManifestResult> GetManifestAsync(string image, IRegistryCredentialsHost credsHost, bool isDryRun)
        {
            if (isDryRun)
            {
                return Task.FromResult(new ManifestResult("", new JsonObject()));
            }

            ImageName imageName = ImageName.Parse(image, autoResolveRepoName: true);

            BasicAuthenticationCredentials? basicAuthCreds = null;

            // Lookup the credentials, if any, for the registry where the image is located
            string credsRegistry = imageName.Registry ?? DockerHelper.DockerHubRegistry;
            if (credsHost.Credentials.TryGetValue(credsRegistry, out RegistryCredentials? registryCreds))
            {
                basicAuthCreds = new BasicAuthenticationCredentials
                {
                    UserName = registryCreds.Username,
                    Password = registryCreds.Password
                };
            }

            RegistryServiceClient registryClient = new(
                imageName.Registry ?? DockerHelper.DockerHubApiRegistry,
                _httpClient,
                basicAuthCreds);
            return registryClient.GetManifestAsync(imageName.Repo, (imageName.Tag ?? imageName.Digest)!);
        }
    }
}
#nullable disable
