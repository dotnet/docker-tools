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

        public Task<ManifestQueryResult> GetManifestAsync(string image, IRegistryCredentialsHost credsHost, bool isDryRun)
        {
            if (isDryRun)
            {
                return Task.FromResult(new ManifestQueryResult("", new JsonObject()));
            }

            ImageName imageName = ImageName.Parse(image, autoResolveImpliedNames: true);

            BasicAuthenticationCredentials? basicAuthCreds = null;

            // Lookup the credentials, if any, for the registry where the image is located
            if (credsHost.Credentials.TryGetValue(imageName.Registry!, out RegistryCredentials? registryCreds))
            {
                basicAuthCreds = new BasicAuthenticationCredentials
                {
                    UserName = registryCreds.Username,
                    Password = registryCreds.Password
                };
            }

            // Docker Hub's registry has a separate host name for its API
            string apiRegistry = imageName.Registry == DockerHelper.DockerHubRegistry ?
                DockerHelper.DockerHubApiRegistry :
                imageName.Registry!;

            RegistryServiceClient registryClient = new(apiRegistry, _httpClient, basicAuthCreds);
            return registryClient.GetManifestAsync(imageName.Repo, (imageName.Tag ?? imageName.Digest)!);
        }
    }
}
#nullable disable
