// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class ManifestToolServiceExtensionsTests
    {
        private const string ManifestDigest = "manifest-digest";
        private const string ManifestListDigest = "manifest-list-digest";

        [Theory]
        [InlineData("tag1", ManifestMediaType.Manifest, ManifestDigest)]
        [InlineData("tag1", ManifestMediaType.ManifestList, null, typeof(InvalidOperationException))]
        [InlineData("tag1", ManifestMediaType.Any, ManifestDigest)]
        [InlineData("tag2", ManifestMediaType.ManifestList, ManifestListDigest)]
        [InlineData("tag2", ManifestMediaType.Manifest, null, typeof(InvalidOperationException))]
        [InlineData("tag2", ManifestMediaType.Any, ManifestListDigest)]
        [InlineData("tag1", (ManifestMediaType)100, null, typeof(ArgumentException))]
        public void GetManifestDigestSha(string tag, ManifestMediaType mediaType, string expectedDigestSha, Type expectedExceptionType = null)
        {
            Mock<IManifestToolService> manifestToolService = new Mock<IManifestToolService>();
            manifestToolService
                .Setup(o => o.Inspect("tag1", false))
                .Returns(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestMediaType, ManifestDigest));
            manifestToolService
                .Setup(o => o.Inspect("tag2", false))
                .Returns(ManifestToolServiceHelper.CreateTagManifest(ManifestToolService.ManifestListMediaType, ManifestListDigest));

            if (expectedExceptionType is null)
            {
                string digestSha = ManifestToolServiceExtensions.GetManifestDigestSha(
                    manifestToolService.Object,
                    mediaType,
                    tag,
                    false);
                Assert.Equal(expectedDigestSha, digestSha);
            }
            else
            {
                Assert.Throws(expectedExceptionType, () =>
                {
                    ManifestToolServiceExtensions.GetManifestDigestSha(
                        manifestToolService.Object,
                        mediaType,
                        tag,
                        false);
                });
            }
        }
    }
}
