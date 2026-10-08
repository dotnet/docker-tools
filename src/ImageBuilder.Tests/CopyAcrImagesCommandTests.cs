// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ImageInfoHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class CopyAcrImagesCommandTests
    {
        public TestContext? TestContext { get; set; }

        private const string SourceRegistry = "my.custom.registry";
        private const string DestinationRegistry = "mcr.microsoft.com";

        /// <summary>
        /// Verifies that image tags associated with a custom Dockerfile will by copied to ACR correctly.
        /// </summary>
        [TestMethod]
        public async Task CopyAcrImagesCommand_CustomDockerfileName()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                Mock<ICopyImageService> copyImageServiceMock = new();

                CopyAcrImagesCommand command = new(
                    TestHelper.CreateManifestJsonService(),
                    copyImageServiceMock.Object,
                    Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                    TestHelper.CreateArtifactService(tempFolderContext.Path));
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
                command.Options.SourceRegistry = SourceRegistry;
                command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile.custom");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo("runtime",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag1", "tag2" })))
                );
                manifest.Registry = DestinationRegistry;

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                RepoData runtimeRepo;

                ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
                {
                    Repos =
                    {
                        {
                            runtimeRepo = new RepoData
                            {
                                Repo = "runtime",
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                PathHelper.NormalizePath(dockerfileRelativePath),
                                                simpleTags: new List<string>
                                                {
                                                    "tag1",
                                                    "tag2"
                                                })
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

                command.LoadManifest();
                await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

                IList<string> expectedTags = runtimeRepo.Images.First().Platforms.First().SimpleTags
                    .Select(tag => $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:{tag}")
                    .ToList();

                foreach (string expectedTag in expectedTags)
                {
                    copyImageServiceMock.Verify(o =>
                        o.ImportImageAsync(
                            new string[] { expectedTag },
                            manifest.Registry,
                            expectedTag,
                            true,
                            It.IsAny<CancellationToken>(),
                            SourceRegistry,
                            null,
                            false));
                }

                copyImageServiceMock.VerifyNoOtherCalls();
            }
        }

        /// <summary>
        /// Verifies that image tags associated with a Dockerfile that is shared by more than one platform are copied.
        /// </summary>
        [TestMethod]
        public async Task CopyAcrImagesCommand_SharedDockerfile()
        {
            using (TempFolderContext tempFolderContext = TestHelper.UseTempFolder())
            {
                var copyImageServiceMock = new Mock<ICopyImageService>();

                var command = new CopyAcrImagesCommand(
                    TestHelper.CreateManifestJsonService(),
                    copyImageServiceMock.Object,
                    Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                    TestHelper.CreateArtifactService(tempFolderContext.Path));
                command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
                command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
                command.Options.SourceRegistry = SourceRegistry;
                command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

                const string runtimeRelativeDir = "1.0/runtime/os";
                Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
                string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
                File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

                Manifest manifest = ManifestHelper.CreateManifest(
                    ManifestHelper.CreateRepo("runtime",
                        ManifestHelper.CreateImage(
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag1a", "tag1b" }, osVersion: "alpine3.10"),
                            ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag2a" }, osVersion: "alpine3.11")))
                );
                manifest.Registry = DestinationRegistry;

                File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

                RepoData runtimeRepo;

                ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
                {
                    Repos =
                    {
                        {
                            runtimeRepo = new RepoData
                            {
                                Repo = "runtime",
                                Images =
                                {
                                    new ImageData
                                    {
                                        Platforms =
                                        {
                                            CreatePlatform(
                                                PathHelper.NormalizePath(dockerfileRelativePath),
                                                simpleTags: new List<string>
                                                {
                                                    "tag1a",
                                                    "tag1b"
                                                },
                                                osVersion: "alpine3.10"),
                                            CreatePlatform(
                                                PathHelper.NormalizePath(dockerfileRelativePath),
                                                simpleTags: new List<string>
                                                {
                                                    "tag2a"
                                                },
                                                osVersion: "alpine3.11")
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

                command.LoadManifest();
                await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

                List<string> expectedTags = new List<string>
                {
                    $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag1a",
                    $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag1b",
                    $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag2a"
                };

                foreach (string expectedTag in expectedTags)
                {
                    copyImageServiceMock.Verify(o =>
                        o.ImportImageAsync(
                            new string[] { expectedTag },
                            manifest.Registry,
                            expectedTag,
                            true,
                            It.IsAny<CancellationToken>(),
                            SourceRegistry,
                            null,
                            false));
                }

                copyImageServiceMock.VerifyNoOtherCalls();
            }
        }

        /// <summary>
        /// Verifies that image tags associated with a runtime-deps Dockerfiles that is shared by multiple versions.
        /// </summary>
        [TestMethod]
        public async Task CopyAcrImagesCommand_RuntimeDepsSharing()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            var copyImageServiceMock = new Mock<ICopyImageService>();

            var command = new CopyAcrImagesCommand(
                TestHelper.CreateManifestJsonService(),
                copyImageServiceMock.Object,
                Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                TestHelper.CreateArtifactService(tempFolderContext.Path));
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
            command.Options.SourceRegistry = SourceRegistry;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfileRelativePath = DockerfileHelper.CreateDockerfile("3.1/runtime-deps/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo("runtime-deps",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRelativePath, new string[] { "3.1" }, osVersion: "noble")
                        },
                        productVersion: "3.1"),
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRelativePath, new string[] { "5.0" }, osVersion: "noble")
                        },
                        productVersion: "5.0"))
            );
            manifest.Registry = DestinationRegistry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            RepoData runtimeRepo;

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        runtimeRepo = new RepoData
                        {
                            Repo = "runtime-deps",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfileRelativePath),
                                            simpleTags: new List<string>
                                            {
                                                "3.1"
                                            },
                                            osVersion: "noble")
                                    },
                                    ProductVersion = "3.1"
                                },
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfileRelativePath),
                                            simpleTags: new List<string>
                                            {
                                                "5.0"
                                            },
                                            osVersion: "noble")
                                    },
                                    ProductVersion = "5.0"
                                }
                            }
                        }
                    }
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            List<string> expectedTags = new List<string>
            {
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:3.1",
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:5.0"
            };

            foreach (string expectedTag in expectedTags)
            {
                copyImageServiceMock.Verify(o =>
                        o.ImportImageAsync(
                            new string[] { expectedTag },
                            manifest.Registry,
                            expectedTag,
                            true,
                            It.IsAny<CancellationToken>(),
                            SourceRegistry,
                            null,
                            false));
            }

            copyImageServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies that image tags can be syndicated to another repo.
        /// </summary>
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task SyndicatedTags(bool isDryRun)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            var copyImageServiceMock = new Mock<ICopyImageService>();

            var command = new CopyAcrImagesCommand(
                TestHelper.CreateManifestJsonService(),
                copyImageServiceMock.Object,
                Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                TestHelper.CreateArtifactService(tempFolderContext.Path));
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.SourceRepoPrefix = "build/";
            command.Options.RepoPrefix = "test/";
            command.Options.IsDryRun = isDryRun;
            command.Options.SourceRegistry = SourceRegistry;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            const string runtimeRelativeDir = "1.0/runtime/os";
            Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, runtimeRelativeDir));
            string dockerfileRelativePath = Path.Combine(runtimeRelativeDir, "Dockerfile");
            File.WriteAllText(Path.Combine(tempFolderContext.Path, dockerfileRelativePath), "FROM repo:tag");

            Manifest manifest = ManifestHelper.CreateManifest(
                ManifestHelper.CreateRepo("runtime",
                    ManifestHelper.CreateImage(
                        ManifestHelper.CreatePlatform(dockerfileRelativePath, new string[] { "tag1-$(stamp)", "tag2", "tag3" })))
            );
            manifest.Registry = DestinationRegistry;

            const string syndicatedRepo = "runtime2";
            manifest.Repos[0].Images[0].Syndication = "$(destination)";
            AddVariable(manifest, "destination", syndicatedRepo);
            AddVariable(manifest, "stamp", "now");

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest), JsonConvert.SerializeObject(manifest));

            RepoData runtimeRepo;

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    {
                        runtimeRepo = new RepoData
                        {
                            Repo = "runtime",
                            Images =
                            {
                                new ImageData
                                {
                                    Platforms =
                                    {
                                        CreatePlatform(
                                            PathHelper.NormalizePath(dockerfileRelativePath),
                                            simpleTags: new List<string>
                                            {
                                                "tag1-built",
                                                "tag2",
                                                "tag3"
                                            })
                                    }
                                }
                            }
                        }
                    }
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            List<string> expectedTags = new List<string>
            {
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag1-built",
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag2",
                $"{command.Options.RepoPrefix}{runtimeRepo.Repo}:tag3",
                $"{command.Options.RepoPrefix}{syndicatedRepo}:tag1-built",
                $"{command.Options.RepoPrefix}{syndicatedRepo}:tag2",
                $"{command.Options.RepoPrefix}{syndicatedRepo}:tag3"
            };

            foreach (string expectedTag in expectedTags)
            {
                copyImageServiceMock.Verify(o =>
                        o.ImportImageAsync(
                            new string[] { expectedTag },
                            manifest.Registry,
                            $"build/runtime:{expectedTag.Split(':')[1]}",
                            true,
                            It.IsAny<CancellationToken>(),
                            SourceRegistry,
                            null,
                            isDryRun));
            }

            copyImageServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies that manifest list shared tags are copied alongside platform tags.
        /// </summary>
        [TestMethod]
        public async Task CopyAcrImagesCommand_CopiesManifestListTags()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<ICopyImageService> copyImageServiceMock = new();

            CopyAcrImagesCommand command = new(
                TestHelper.CreateManifestJsonService(),
                copyImageServiceMock.Object,
                Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                TestHelper.CreateArtifactService(tempFolderContext.Path));
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
            command.Options.SourceRegistry = SourceRegistry;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfileRelativePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRelativePath, new string[] { "tag1" })
                        },
                        new Dictionary<string, Tag>
                        {
                            { "shared1", new Tag() },
                            { "shared2", new Tag() }
                        }))
            );
            manifest.Registry = DestinationRegistry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest),
                JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(
                                        PathHelper.NormalizePath(dockerfileRelativePath),
                                        simpleTags: new List<string> { "tag1" })
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags = { "shared1", "shared2" }
                                }
                            }
                        }
                    }
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            // Platform tag should be copied
            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { $"test/runtime:tag1" },
                    DestinationRegistry,
                    "test/runtime:tag1",
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            // Manifest list shared tags should also be copied
            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { $"test/runtime:shared1" },
                    DestinationRegistry,
                    "test/runtime:shared1",
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { $"test/runtime:shared2" },
                    DestinationRegistry,
                    "test/runtime:shared2",
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            copyImageServiceMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies that syndicated manifest list shared tags are copied to the syndicated repo.
        /// </summary>
        [TestMethod]
        public async Task CopyAcrImagesCommand_CopiesSyndicatedManifestListTags()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<ICopyImageService> copyImageServiceMock = new();

            CopyAcrImagesCommand command = new(
                TestHelper.CreateManifestJsonService(),
                copyImageServiceMock.Object,
                Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                TestHelper.CreateArtifactService(tempFolderContext.Path));
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
            command.Options.SourceRegistry = SourceRegistry;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfileRelativePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(dockerfileRelativePath, new string[] { "tag1" })
                        },
                        new Dictionary<string, Tag>
                        {
                            {
                                "shared1-$(stamp)",
                                new Tag()
                            }
                        }))
            );
            manifest.Registry = DestinationRegistry;
            manifest.Repos[0].Images[0].Syndication = "runtime2";
            AddVariable(manifest, "stamp", "now");

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest),
                JsonConvert.SerializeObject(manifest));

            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(
                                        PathHelper.NormalizePath(dockerfileRelativePath),
                                        simpleTags: new List<string> { "tag1" })
                                },
                                Manifest = new ManifestData
                                {
                                    SharedTags = { "shared1-built" }
                                }
                            }
                        }
                    }
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            // Platform tag
            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { $"test/runtime:tag1" },
                    DestinationRegistry,
                    "test/runtime:tag1",
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            // Primary manifest list shared tag
            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { $"test/runtime:shared1-built" },
                    DestinationRegistry,
                    "test/runtime:shared1-built",
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { "test/runtime2:tag1" },
                    DestinationRegistry,
                    "test/runtime:tag1",
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { $"test/runtime2:shared1-built" },
                    DestinationRegistry,
                    "test/runtime:shared1-built",
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            copyImageServiceMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task CopyAcrImagesCommand_OnlySyndicatesConfiguredImages()
        {
            using TempFolderContext context = TestHelper.UseTempFolder();

            string dockerfile1 = DockerfileHelper.CreateDockerfile("image1", context);
            string dockerfile2 = DockerfileHelper.CreateDockerfile("image2", context);
            string dockerfile3 = DockerfileHelper.CreateDockerfile("image3", context);

            Image syndicatedImage = CreateImage(
                ["shared"],
                ManifestHelper.CreatePlatform(dockerfile1, ["amd64"]),
                ManifestHelper.CreatePlatform(dockerfile2, ["arm64"], architecture: Architecture.ARM64));
            syndicatedImage.Syndication = "syndicated";

            Manifest manifest = CreateManifest(CreateRepo(
                "repo", syndicatedImage, CreateImage(ManifestHelper.CreatePlatform(dockerfile3, ["other"]))));
            manifest.Registry = DestinationRegistry;

            ImageArtifactDetails imageInfo = CreateImageArtifactDetails(CreateRepoData(
                "repo",
                CreateImageData(
                    ["shared"],
                    CreatePlatform(dockerfile1, simpleTags: ["amd64"]),
                    CreatePlatform(dockerfile2, simpleTags: ["arm64"], architecture: "arm64")),
                CreateImageData(CreatePlatform(dockerfile3, simpleTags: ["other"]))));

            Mock<ICopyImageService> copyService = new();

            CopyAcrImagesCommand command = new(
                TestHelper.CreateManifestJsonService(),
                copyService.Object,
                Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                TestHelper.CreateArtifactService(context.Path));

            command.Options.Manifest = Path.Combine(context.Path, "manifest.json");
            command.Options.ImageInfoPath = Path.Combine(context.Path, "image-info.json");
            command.Options.SourceRegistry = SourceRegistry;
            command.Options.SourceRepoPrefix = "build/";
            command.Options.RepoPrefix = "publish/";

            File.WriteAllText(command.Options.Manifest, JsonHelper.SerializeObject(manifest));
            File.WriteAllText(command.Options.ImageInfoPath, JsonHelper.SerializeObject(imageInfo));

            command.LoadManifest();

            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            foreach (string tag in new[] { "amd64", "arm64", "shared", "other" })
            {
                copyService.Verify(service => service.ImportImageAsync(
                    new[] { $"publish/repo:{tag}" },
                    DestinationRegistry, $"build/repo:{tag}", true,
                    It.IsAny<CancellationToken>(), SourceRegistry, null, false));
            }

            foreach (string tag in new[] { "amd64", "arm64", "shared" })
            {
                copyService.Verify(service => service.ImportImageAsync(
                    new[] { $"publish/syndicated:{tag}" },
                    DestinationRegistry, $"build/repo:{tag}", true,
                    It.IsAny<CancellationToken>(), SourceRegistry, null, false));
            }

            copyService.VerifyNoOtherCalls();
        }

        /// <summary>
        /// Verifies that images without ManifestData do not produce manifest list tag copies.
        /// </summary>
        [TestMethod]
        public async Task CopyAcrImagesCommand_SkipsManifestListsWithNoManifestData()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            Mock<ICopyImageService> copyImageServiceMock = new();

            CopyAcrImagesCommand command = new(
                TestHelper.CreateManifestJsonService(),
                copyImageServiceMock.Object,
                Mock.Of<ILogger<CopyAcrImagesCommand>>(),
                TestHelper.CreateArtifactService(tempFolderContext.Path));
            command.Options.Manifest = Path.Combine(tempFolderContext.Path, "manifest.json");
            command.Options.SourceRepoPrefix = command.Options.RepoPrefix = "test/";
            command.Options.SourceRegistry = SourceRegistry;
            command.Options.ImageInfoPath = Path.Combine(tempFolderContext.Path, "image-info.json");

            string dockerfileRelativePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            Manifest manifest = CreateManifest(
                CreateRepo("runtime",
                    CreateImage(
                        CreatePlatform(dockerfileRelativePath, new string[] { "tag1" })))
            );
            manifest.Registry = DestinationRegistry;

            File.WriteAllText(Path.Combine(tempFolderContext.Path, command.Options.Manifest),
                JsonConvert.SerializeObject(manifest));

            // No ManifestData on the image - only platform tags
            ImageArtifactDetails imageArtifactDetails = new ImageArtifactDetails
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "runtime",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    CreatePlatform(
                                        PathHelper.NormalizePath(dockerfileRelativePath),
                                        simpleTags: new List<string> { "tag1" })
                                }
                            }
                        }
                    }
                }
            };

            File.WriteAllText(command.Options.ImageInfoPath, JsonConvert.SerializeObject(imageArtifactDetails));

            command.LoadManifest();
            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            // Only platform tag should be copied - no manifest list tags
            copyImageServiceMock.Verify(o =>
                o.ImportImageAsync(
                    new string[] { $"test/runtime:tag1" },
                    DestinationRegistry,
                    It.IsAny<string>(),
                    true,
                    It.IsAny<CancellationToken>(),
                    SourceRegistry,
                    null,
                    false));

            copyImageServiceMock.VerifyNoOtherCalls();
        }
    }
}
