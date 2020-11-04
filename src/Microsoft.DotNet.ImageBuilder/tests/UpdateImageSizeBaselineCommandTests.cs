// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Valleysoft.DockerfileModel;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class UpdateImageSizeBaselineCommandTests
    {
        private const string RuntimeDepsRepo = "runtime-deps";
        private const string RuntimeRepo = "runtime";
        private const string SdkRepo = "sdk";
        private const string RuntimeDepsRelativeDir = "3.1/runtime-deps/os";
        private const string RuntimeRelativeDir = "3.1/runtime/os";
        private const string SdkRelativeDir = "3.1/sdk/os";
        private const string RuntimeDepsTag = "tag";
        private const string RuntimeTag = "tag";
        private const string SdkTag = "tag";

        /// <summary>
        /// Verifies that all baseline values are updated.
        /// </summary>
        [Fact]
        public async Task UpdateAllImages()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Manifest manifest = CreateTestData(tempFolderContext);

            const int RuntimeDepsSize = 1;
            const int RuntimeSize = 2;
            const int SdkSize = 3;

            Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
            dockerServiceMock
                .Setup(o => o.GetImageSize(GetTag(RuntimeDepsRepo, RuntimeDepsTag), It.IsAny<bool>()))
                .Returns(RuntimeDepsSize);
            dockerServiceMock
                .Setup(o => o.GetImageSize(GetTag(RuntimeRepo, RuntimeTag), It.IsAny<bool>()))
                .Returns(RuntimeSize);
            dockerServiceMock
                .Setup(o => o.GetImageSize(GetTag(SdkRepo, SdkTag), It.IsAny<bool>()))
                .Returns(SdkSize);
            dockerServiceMock
                    .Setup(o => o.LocalImageExists(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(true);

            UpdateImageSizeBaselineCommand command = new UpdateImageSizeBaselineCommand(
                dockerServiceMock.Object, Mock.Of<ILoggerService>());
            command.Options.BaselinePath = Path.Combine(tempFolderContext.Path, "baseline.json");
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.AllBaselineData = true;

            // Write manifest file
            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            command.LoadManifest();
            await command.ExecuteAsync();

            JObject expectedBaseline = CreateBaselineJson(RuntimeDepsSize, RuntimeSize, SdkSize);
            string actualBaselineText = File.ReadAllText(command.Options.BaselinePath);

            Assert.Equal(expectedBaseline.ToString(), actualBaselineText);
        }

        /// <summary>
        /// Verifies that, when the OutOfRangeOnly options is enabled, only baseline size for images that are out of the
        /// allowed range get updated.
        /// </summary>
        [Fact]
        public async Task UpdateOutOfRangeImagesOnly()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            Manifest manifest = CreateTestData(tempFolderContext);

            const long ActualRuntimeDepsSize = 105;
            const long ActualRuntimeSize = 111;
            const long ActualSdkSize = 95;

            Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
            dockerServiceMock
                .Setup(o => o.GetImageSize(GetTag(RuntimeDepsRepo, RuntimeDepsTag), It.IsAny<bool>()))
                .Returns(ActualRuntimeDepsSize);
            dockerServiceMock
                .Setup(o => o.GetImageSize(GetTag(RuntimeRepo, RuntimeTag), It.IsAny<bool>()))
                .Returns(ActualRuntimeSize);
            dockerServiceMock
                .Setup(o => o.GetImageSize(GetTag(SdkRepo, SdkTag), It.IsAny<bool>()))
                .Returns(ActualSdkSize);
            dockerServiceMock
                    .Setup(o => o.LocalImageExists(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(true);

            UpdateImageSizeBaselineCommand command = new UpdateImageSizeBaselineCommand(
                dockerServiceMock.Object, Mock.Of<ILoggerService>());
            command.Options.BaselinePath = Path.Combine(tempFolderContext.Path, "baseline.json");
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.AllBaselineData = false;
            command.Options.AllowedVariance = 10;

            // Write manifest file
            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            // Write original baseline file
            const long DefaultOriginalBaselineSize = 100;
            JObject originalBaseline = CreateBaselineJson(
                DefaultOriginalBaselineSize, DefaultOriginalBaselineSize, DefaultOriginalBaselineSize);
            File.WriteAllText(command.Options.BaselinePath, originalBaseline.ToString());

            command.LoadManifest();
            await command.ExecuteAsync();

            // Only the runtime size should be updated since it was the only one out of range
            JObject expectedBaseline = CreateBaselineJson(
                DefaultOriginalBaselineSize, ActualRuntimeSize, DefaultOriginalBaselineSize);

            string actualBaselineText = File.ReadAllText(command.Options.BaselinePath);

            Assert.Equal(expectedBaseline.ToString(), actualBaselineText);
        }

        private static string GetTag(string repo, string tag) => new ImageName(repo, tag: tag).ToString();

        private static JObject CreateBaselineJson(long runtimeDepsSize, long runtimeSize, long sdkSize)
        {
            JObject runtimeDepsBaseline = new JObject();
            runtimeDepsBaseline[RuntimeDepsRelativeDir] = runtimeDepsSize;

            JObject runtimeBaseline = new JObject();
            runtimeBaseline[RuntimeRelativeDir] = runtimeSize;

            JObject sdkBaseline = new JObject();
            sdkBaseline[SdkRelativeDir] = sdkSize;

            JObject baseline = new JObject();
            baseline[RuntimeDepsRepo] = runtimeDepsBaseline;
            baseline[RuntimeRepo] = runtimeBaseline;
            baseline[SdkRepo] = sdkBaseline;
            return baseline;
        }

        private static Manifest CreateTestData(TempFolderContext tempFolderContext)
        {
            CreateDockerfile(RuntimeDepsRelativeDir, tempFolderContext);
            CreateDockerfile(RuntimeRelativeDir, tempFolderContext, GetTag(RuntimeDepsRepo, RuntimeDepsTag));
            CreateDockerfile(SdkRelativeDir, tempFolderContext, "base");

            return CreateManifest(
                CreateRepo(RuntimeDepsRepo,
                    CreateImage(
                        CreatePlatform(RuntimeDepsRelativeDir, new string[] { RuntimeDepsTag }))),
                CreateRepo(RuntimeRepo,
                    CreateImage(
                        CreatePlatform(RuntimeRelativeDir, new string[] { RuntimeTag }))),
                CreateRepo(SdkRepo,
                    CreateImage(
                        CreatePlatform(SdkRelativeDir, new string[] { SdkTag })))
            );
        }
    }
}
