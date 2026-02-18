// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

public class CopyImageServiceTests
{
    /// <summary>
    /// When isDryRun is true and the publish configuration has no registry authentication,
    /// ImportImageAsync should succeed without throwing. This scenario occurs in PR validation
    /// pipelines where appsettings.json is not generated.
    /// </summary>
    [Fact]
    public async Task ImportImageAsync_DryRun_DoesNotRequirePublishConfiguration()
    {
        var emptyConfig = new PublishConfiguration();
        var service = new CopyImageService(
            Mock.Of<ILogger<CopyImageService>>(),
            Mock.Of<IAzureTokenCredentialProvider>(),
            ConfigurationHelper.CreateOptionsMock(emptyConfig));

        await Should.NotThrowAsync(() =>
            service.ImportImageAsync(
                destTagNames: ["myacr.azurecr.io/repo:tag"],
                destAcrName: "myacr.azurecr.io",
                srcTagName: "repo:tag",
                srcRegistryName: "docker.io",
                isDryRun: true));
    }
}
