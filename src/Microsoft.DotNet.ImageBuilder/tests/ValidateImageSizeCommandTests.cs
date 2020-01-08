// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    public class ValidateImageSizeCommandTests
    {
        private const string RuntimeDepsRepo = "runtime-deps";
        private const string RuntimeRepo = "runtime";
        private const string AspnetRepo = "aspnet";
        private const string SdkRepo = "sdk";
        private const string RuntimeDeps1RelativeDir = "3.1/runtime-deps/os1";
        private const string RuntimeDeps2RelativeDir = "3.1/runtime-deps/os2";
        private const string RuntimeRelativeDir = "3.1/runtime/os";
        private const string AspnetRelativeDir = "3.1/aspnet/os";
        private const string SdkRelativeDir = "3.1/sdk/os";
        private const string RuntimeDeps1Tag = "tag";
        private const string RuntimeDeps2Tag = "tag2";
        private const string RuntimeTag = "tag";
        private const string AspnetTag = "tag";
        private const string SdkTag = "tag";

        /// <summary>
        /// Verifies no validation errors occur when there are no image size differences amongst the images.
        /// </summary>
        [Fact]
        public async Task NoSizeDifferences()
        {
            ImageSizeData[] imageSizes = new []
            {
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps1RelativeDir, RuntimeDeps1Tag, 1, 1),
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps2RelativeDir, RuntimeDeps2Tag, 2, 2),
                new ImageSizeData(RuntimeRepo, RuntimeRelativeDir, RuntimeTag, 3, 3),
                new ImageSizeData(AspnetRepo, AspnetRelativeDir, AspnetTag, 4, 4),
                new ImageSizeData(SdkRepo, SdkRelativeDir, SdkTag, 5, 5)
            };

            TestContext testContext = new TestContext(imageSizes);
            ValidateImageSizeCommand command = await testContext.RunTestAsync();

            testContext.Verify(isValidationErrorExpected: false);

            ImageSizeValidationResults results = command.ValidationResults;

            Assert.Equal(5, results.ImagesWithNoSizeChange.Count());
            Assert.Empty(results.ImagesWithAllowedSizeChange);
            Assert.Empty(results.ImagesWithDisallowedSizeChange);
            Assert.Empty(results.ImagesWithMissingBaseline);
            Assert.Empty(results.ImagesWithExtraneousBaseline);
        }

        /// <summary>
        /// Verifies the validation correctly classifies "allowed" vs "disallowed" image size differences.
        /// </summary>
        [Fact]
        public async Task SizeDifferences()
        {
            const int AllowedVariance = 5;

            // Define the test data such that some of the image sizes are within the allowed range and others are
            // just out of the allowed range. These numbers are all in relation to the AllowedVariance.
            ImageSizeData[] imageSizes = new[]
            {
                // Within range (under)
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps1RelativeDir, RuntimeDeps1Tag, 100, 95),

                // Out of range (under)
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps2RelativeDir, RuntimeDeps2Tag, 100, 94),

                // Within range (over)
                new ImageSizeData(RuntimeRepo, RuntimeRelativeDir, RuntimeTag, 100, 101),

                // Out of range (over)
                new ImageSizeData(AspnetRepo, AspnetRelativeDir, AspnetTag, 100, 106),

                // Same size
                new ImageSizeData(SdkRepo, SdkRelativeDir, SdkTag, 100, 100)
            };

            TestContext testContext = new TestContext(imageSizes, AllowedVariance);
            ValidateImageSizeCommand command = await testContext.RunTestAsync();

            testContext.Verify(isValidationErrorExpected: true);

            ImageSizeValidationResults results = command.ValidationResults;

            Assert.Single(results.ImagesWithNoSizeChange);
            Assert.Equal(SdkRelativeDir, results.ImagesWithNoSizeChange.First().Id);
            Assert.Equal(2, results.ImagesWithAllowedSizeChange.Count());
            Assert.Equal(RuntimeDeps1RelativeDir, results.ImagesWithAllowedSizeChange.ElementAt(0).Id);
            Assert.Equal(RuntimeRelativeDir, results.ImagesWithAllowedSizeChange.ElementAt(1).Id);
            Assert.Equal(2, results.ImagesWithDisallowedSizeChange.Count());
            Assert.Equal(RuntimeDeps2RelativeDir, results.ImagesWithDisallowedSizeChange.ElementAt(0).Id);
            Assert.Equal(AspnetRelativeDir, results.ImagesWithDisallowedSizeChange.ElementAt(1).Id);
            Assert.Empty(results.ImagesWithMissingBaseline);
            Assert.Empty(results.ImagesWithExtraneousBaseline);
        }

        /// <summary>
        /// Verifies the validation identifies images that have missing baseline data.
        /// </summary>
        [Fact]
        public async Task MissingBaseline()
        {
            // Specifying null for the baseline size prevents the image data from being set in the baseline file. But since
            // an actual size is set, the image will be defined in the manifest. This is the scenario that we want to identify
            // because baseline data is missing for an image defined in the manifest.
            ImageSizeData[] imageSizes = new[]
            {
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps1RelativeDir, RuntimeDeps1Tag, 1, 1),
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps2RelativeDir, RuntimeDeps2Tag, baselineSize: null, actualSize: 2),
                new ImageSizeData(RuntimeRepo, RuntimeRelativeDir, RuntimeTag, 3, 3),
                new ImageSizeData(AspnetRepo, AspnetRelativeDir, AspnetTag, 4, 4),
                new ImageSizeData(SdkRepo, SdkRelativeDir, SdkTag, baselineSize: null, actualSize: 5)
            };

            TestContext testContext = new TestContext(imageSizes);
            ValidateImageSizeCommand command = await testContext.RunTestAsync();

            testContext.Verify(isValidationErrorExpected: true);

            ImageSizeValidationResults results = command.ValidationResults;

            Assert.Equal(3, results.ImagesWithNoSizeChange.Count());
            Assert.Equal(RuntimeDeps1RelativeDir, results.ImagesWithNoSizeChange.ElementAt(0).Id);
            Assert.Equal(RuntimeRelativeDir, results.ImagesWithNoSizeChange.ElementAt(1).Id);
            Assert.Equal(AspnetRelativeDir, results.ImagesWithNoSizeChange.ElementAt(2).Id);
            Assert.Empty(results.ImagesWithAllowedSizeChange);
            Assert.Empty(results.ImagesWithDisallowedSizeChange);
            Assert.Equal(2, results.ImagesWithMissingBaseline.Count());
            Assert.Equal(RuntimeDeps2RelativeDir, results.ImagesWithMissingBaseline.ElementAt(0).Id);
            Assert.Equal(SdkRelativeDir, results.ImagesWithMissingBaseline.ElementAt(1).Id);
            Assert.Empty(results.ImagesWithExtraneousBaseline);
        }

        /// <summary>
        /// Verifies the validation identifies extraneous baseline data for which there is no image defined.
        /// </summary>
        [Fact]
        public async Task ExtraneousBaseline()
        {
            // Specifying null for the actual size prevents the image from being defined in the manifest. But since
            // a baseline size is defined, it'll end up in the baseline file. This is the scenario that we want to
            // identify because there will be extraneous data in the baseline file for which no image exists.
            ImageSizeData[] imageSizes = new[]
            {
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps1RelativeDir, RuntimeDeps1Tag, 1, actualSize: null),
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps2RelativeDir, RuntimeDeps2Tag, 2, 2),
                new ImageSizeData(RuntimeRepo, RuntimeRelativeDir, RuntimeTag, 3, 3),
                new ImageSizeData(AspnetRepo, AspnetRelativeDir, AspnetTag, 4, actualSize: null),
                new ImageSizeData(SdkRepo, SdkRelativeDir, SdkTag, 5, 5)
            };

            TestContext testContext = new TestContext(imageSizes);
            ValidateImageSizeCommand command = await testContext.RunTestAsync();

            testContext.Verify(isValidationErrorExpected: true);

            ImageSizeValidationResults results = command.ValidationResults;

            Assert.Equal(3, results.ImagesWithNoSizeChange.Count());
            Assert.Equal(RuntimeDeps2RelativeDir, results.ImagesWithNoSizeChange.ElementAt(0).Id);
            Assert.Equal(RuntimeRelativeDir, results.ImagesWithNoSizeChange.ElementAt(1).Id);
            Assert.Equal(SdkRelativeDir, results.ImagesWithNoSizeChange.ElementAt(2).Id);
            Assert.Empty(results.ImagesWithAllowedSizeChange);
            Assert.Empty(results.ImagesWithDisallowedSizeChange);
            Assert.Empty(results.ImagesWithMissingBaseline);
            Assert.Equal(2, results.ImagesWithExtraneousBaseline.Count());
            Assert.Equal(RuntimeDeps1RelativeDir, results.ImagesWithExtraneousBaseline.ElementAt(0).Id);
            Assert.Equal(AspnetRelativeDir, results.ImagesWithExtraneousBaseline.ElementAt(1).Id);
        }

        /// <summary>
        /// Verifies that only new images (those missing baseline data) or old images (those with extraneous
        /// baseline data) are validated.
        /// </summary>
        [Fact]
        public async Task CheckBaselineIntegrityOnly()
        {
            const int AllowedVariance = 5;
            ImageSizeData[] imageSizes = new[]
            {
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps1RelativeDir, RuntimeDeps1Tag, 100, 99),
                new ImageSizeData(RuntimeDepsRepo, RuntimeDeps2RelativeDir, RuntimeDeps2Tag, 2, actualSize: null),
                new ImageSizeData(RuntimeRepo, RuntimeRelativeDir, RuntimeTag, 3, 3),
                new ImageSizeData(AspnetRepo, AspnetRelativeDir, AspnetTag, 100, 200),
                new ImageSizeData(SdkRepo, SdkRelativeDir, SdkTag, baselineSize: null, 5)
            };

            // This will configure the test so that an exception will be thrown should the logic ever attempt to
            // retrieve an image size. Image sizes should not be retrieved when we're only validating baseline integrity.
            bool localImagesExist = false;

            TestContext testContext = new TestContext(imageSizes, AllowedVariance, checkBaselineIntegrityOnly: true,
                localImagesExist: localImagesExist);
            ValidateImageSizeCommand command = await testContext.RunTestAsync();

            testContext.Verify(isValidationErrorExpected: true);

            ImageSizeValidationResults results = command.ValidationResults;

            Assert.Empty(results.ImagesWithNoSizeChange);
            Assert.Empty(results.ImagesWithAllowedSizeChange);
            Assert.Empty(results.ImagesWithDisallowedSizeChange);
            Assert.Single(results.ImagesWithMissingBaseline);
            Assert.Equal(SdkRelativeDir, results.ImagesWithMissingBaseline.First().Id);
            Assert.Single(results.ImagesWithExtraneousBaseline);
            Assert.Equal(RuntimeDeps2RelativeDir, results.ImagesWithExtraneousBaseline.First().Id);
        }

        private class TestContext
        {
            private readonly IEnumerable<ImageSizeData> imageSizes;
            private readonly int? allowedVariance;
            private readonly bool localImagesExist;
            private readonly bool checkBaselineIntegrityOnly;
            private readonly Mock<IEnvironmentService> environmentServiceMock;

            public TestContext(IEnumerable<ImageSizeData> imageSizes, int? allowedVariance = null, bool? checkBaselineIntegrityOnly = false,
                bool? localImagesExist = true)
            {
                this.imageSizes = imageSizes;
                this.allowedVariance = allowedVariance;
                this.localImagesExist = localImagesExist == true;
                this.checkBaselineIntegrityOnly = checkBaselineIntegrityOnly == true;
                this.environmentServiceMock = new Mock<IEnvironmentService>();
            }

            public async Task<ValidateImageSizeCommand> RunTestAsync()
            {
                using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

                // Use the image size data defined by the test case to generate a baseline file
                string baselinePath = Path.Combine(tempFolderContext.Path, "baseline.json");
                CreateBaselineFile(baselinePath, this.imageSizes);

                // Use the image size data defined by the test to provide mock values for the image sizes
                // from the DockerService.
                Mock<IDockerService> dockerServiceMock = new Mock<IDockerService>();
                dockerServiceMock
                    .Setup(o => o.LocalImageExists(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(this.localImagesExist);
                SetupDockerServiceImageSizes(dockerServiceMock, this.imageSizes);

                // Setup the command
                ValidateImageSizeCommand command = new ValidateImageSizeCommand(
                    dockerServiceMock.Object, Mock.Of<ILoggerService>(), environmentServiceMock.Object);
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.BaselinePath = baselinePath;
                command.Options.AllowedVariance = this.allowedVariance ?? 0;
                command.Options.CheckBaselineIntegrityOnly = this.checkBaselineIntegrityOnly;
                command.Options.IsPullEnabled = false;

                // Use the image size data defined by the test to generate a manifest file
                Manifest manifest = CreateTestManifest(tempFolderContext.Path, this.imageSizes);
                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                // Execute the command
                command.LoadManifest();
                await command.ExecuteAsync();

                return command;
            }

            public void Verify(bool isValidationErrorExpected)
            {
                this.environmentServiceMock.Verify(
                    o => o.Exit(It.IsAny<int>()), isValidationErrorExpected ? Times.Once() : Times.Never());
            }
        }

        private static void SetupDockerServiceImageSizes(
            Mock<IDockerService> dockerServiceMock, IEnumerable<ImageSizeData> imageSizes)
        {
            foreach (ImageSizeData imageSizeData in imageSizes.Where(imageSize => imageSize.ActualSize.HasValue))
            {
                dockerServiceMock
                    .Setup(o => o.GetImageSize(GetTag(imageSizeData.Repo, imageSizeData.ImageTag), It.IsAny<bool>()))
                    .Returns(imageSizeData.ActualSize.Value);
            }
        }

        private static string GetTag(string repo, string tag) => TagInfo.GetFullyQualifiedName(repo, tag);

        private static void CreateBaselineFile(string path, IEnumerable<ImageSizeData> imageSizes)
        {
            JObject json = new JObject();

            foreach (ImageSizeData imageSizeData in imageSizes.Where(imageSize => imageSize.BaselineSize.HasValue))
            {
                JObject repo;
                if (json.TryGetValue(imageSizeData.Repo, out JToken repoToken))
                {
                    repo = (JObject)repoToken;
                }
                else
                {
                    json[imageSizeData.Repo] = repo = new JObject();
                }

                repo[imageSizeData.ImagePath] = imageSizeData.BaselineSize;
            }

            File.WriteAllText(path, json.ToString());
        }

        private static Manifest CreateTestManifest(string basePath, IEnumerable<ImageSizeData> imageSizes)
        {
            // An image is only defined in the manifest if its test data indicates it has an actual image size.
            bool isImageDefined(string imagePath) =>
                imageSizes.Any(imageSize => imageSize.ImagePath == imagePath && imageSize.ActualSize.HasValue);

            List<Repo> repos = new List<Repo>();

            if (isImageDefined(RuntimeDeps1RelativeDir) || isImageDefined(RuntimeDeps2RelativeDir))
            {
                List<Platform> platforms = new List<Platform>();
                if (isImageDefined(RuntimeDeps1RelativeDir))
                {
                    CreateDockerfile(Path.Combine(basePath, RuntimeDeps1RelativeDir), "base");
                    platforms.Add(CreatePlatform(RuntimeDeps1RelativeDir, new string[] { RuntimeDeps1Tag }));
                }

                if (isImageDefined(RuntimeDeps2RelativeDir))
                {
                    CreateDockerfile(Path.Combine(basePath, RuntimeDeps2RelativeDir), "base");
                    platforms.Add(CreatePlatform(RuntimeDeps2RelativeDir, new string[] { RuntimeDeps2Tag }));
                }

                repos.Add(CreateRepo(RuntimeDepsRepo, CreateImage(platforms.ToArray())));
            }

            if (isImageDefined(RuntimeRelativeDir))
            {
                CreateDockerfile(Path.Combine(basePath, RuntimeRelativeDir), GetTag(RuntimeDepsRepo, RuntimeDeps1Tag));
                repos.Add(CreateRepo(RuntimeRepo,
                    CreateImage(
                        CreatePlatform(RuntimeRelativeDir, new string[] { RuntimeTag }))));
            }

            if (isImageDefined(AspnetRelativeDir))
            {
                CreateDockerfile(Path.Combine(basePath, AspnetRelativeDir), GetTag(RuntimeRepo, RuntimeTag));
                repos.Add(CreateRepo(AspnetRepo,
                    CreateImage(
                        CreatePlatform(AspnetRelativeDir, new string[] { AspnetTag }))));
            }

            if (isImageDefined(SdkRelativeDir))
            {
                CreateDockerfile(Path.Combine(basePath, SdkRelativeDir), "base");
                repos.Add(CreateRepo(SdkRepo,
                    CreateImage(
                        CreatePlatform(SdkRelativeDir, new string[] { SdkTag }))));
            }

            return CreateManifest(repos.ToArray());
        }

        private class ImageSizeData
        {
            public ImageSizeData(string repo, string imagePath, string imageTag, long? baselineSize, long? actualSize)
            {
                Repo = repo;
                ImagePath = imagePath;
                ImageTag = imageTag;
                BaselineSize = baselineSize;
                ActualSize = actualSize;
            }

            public string Repo { get; }
            public string ImagePath { get; }
            public string ImageTag { get; }
            public long? BaselineSize { get; }
            public long? ActualSize { get; }
        }
    }
}
