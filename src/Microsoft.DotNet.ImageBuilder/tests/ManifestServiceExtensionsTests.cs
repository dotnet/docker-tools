// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class ManifestServiceExtensionsTests
    {
        private const string ManifestDigest = "manifest-digest";
        private const string ManifestListDigest = "manifest-list-digest";

        [Theory]
        [InlineData("tag1", ManifestDigest)]
        [InlineData("tag2", ManifestListDigest)]
        public async Task GetManifestDigestSha(string tag, string expectedDigestSha)
        {
            RegistryCredentialsOptions credsOptions = new()
            {
                Credentials = new Dictionary<string, RegistryCredentials>
                {
                    { "docker.io", new RegistryCredentials("user", "pwd") }
                }
            };
            Mock<IManifestService> manifestToolService = new Mock<IManifestService>();
            manifestToolService
                .Setup(o => o.GetManifestAsync("tag1", credsOptions, false))
                .ReturnsAsync(new ManifestResult(ManifestDigest, new JsonObject()));
            manifestToolService
                .Setup(o => o.GetManifestAsync("tag2", credsOptions, false))
                .ReturnsAsync(new ManifestResult(ManifestListDigest, new JsonObject()));

            string digestSha = await ManifestServiceExtensions.GetManifestDigestShaAsync(
                manifestToolService.Object,
                tag,
                credsOptions,
                false);
            Assert.Equal(expectedDigestSha, digestSha);
        }
    }
}
