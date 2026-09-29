#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ContainerRegistryHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class AttachLifecycleMetadataCommandTests
    {
        #nullable enable annotations
        public TestContext? TestContext { get; set; }

        #nullable disable annotations

        private const string DefaultRepoPrefix = "public/";
        private const string AcrName = "myacr.azurecr.io";
        private const string McrName = "mcr.microsoft.com";
        private readonly DateOnly _globalDate = DateOnly.FromDateTime(DateTime.UtcNow);
        private readonly ConcurrentBag<string> _annotatedDigests = [];
        private readonly Mock<ILifecycleMetadataService> _lifecycleMetadataServiceMock = new();

        [TestMethod]
        public void AttachLifecycleMetadata_CliNameAndSubcommands()
        {
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(),
                Mock.Of<IAcrContentClientFactory>());

            var cliCommand = command.GetCliCommand();

            cliCommand.Name.ShouldBe("attachLifecycleMetadata");
            cliCommand.Subcommands.Select(subcommand => subcommand.Name).ShouldBe(["published", "all", "file"]);
        }

        [TestMethod]
        [DataRow("file eol.json", "")]
        [DataRow("file eol.json", "--on-conflict skip")]
        [DataRow("all myacr.azurecr.io public/", "")]
        [DataRow("all myacr.azurecr.io public/", "--on-conflict skip")]
        [DataRow("published myacr.azurecr.io public/ old.json new.json", "")]
        [DataRow("published myacr.azurecr.io public/ old.json new.json", "--on-conflict skip")]
        public void AttachLifecycleMetadata_AllSubcommands_BindConflictPolicy(string arguments, string flag)
        {
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(), Mock.Of<IAcrContentClientFactory>());
            var parseResult = command.GetCliCommand().Parse($"{arguments} {flag}");
            parseResult.Errors.ShouldBeEmpty();
            parseResult.GetValue<LifecycleMetadataConflictAction>("--on-conflict").ShouldBe(flag.Length == 0
                ? LifecycleMetadataConflictAction.Error
                : LifecycleMetadataConflictAction.Skip);
        }

        [TestMethod]
        [DataRow("", false, false)]
        [DataRow("--mark-as-internal", true, false)]
        [DataRow("--dry-run", false, true)]
        public async Task AttachLifecycleMetadata_File_AttachesSpecifiedDates(
            string flags, bool markAsInternal, bool dryRun)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string path = Path.Combine(tempFolderContext.Path, "eol.json");
            File.WriteAllText(path, """
                {
                  "eolDate": "2026-01-01",
                  "eolDigests": [
                    { "digest": "myacr.azurecr.io/repo@sha256:first" },
                    { "digest": "myacr.azurecr.io/repo@sha256:second", "eolDate": "2026-02-01" }
                  ]
                }
                """);
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(), Mock.Of<IAcrContentClientFactory>());
            var parseResult = command.GetCliCommand().Parse($"file \"{path}\" --no-version-logging {flags}");
            parseResult.Errors.ShouldBeEmpty();
            (await parseResult.InvokeAsync(cancellationToken: TestContext?.CancellationToken ?? default)).ShouldBe(0);

            _lifecycleMetadataServiceMock.Verify(o => o.AttachLifecycleMetadataAsync(
                $"{AcrName}/repo@sha256:first", new DateOnly(2026, 1, 1), markAsInternal,
                LifecycleMetadataConflictAction.Error,
                It.IsAny<CancellationToken>()), dryRun ? Times.Never() : Times.Once());
            _lifecycleMetadataServiceMock.Verify(o => o.AttachLifecycleMetadataAsync(
                $"{AcrName}/repo@sha256:second", new DateOnly(2026, 2, 1), markAsInternal,
                LifecycleMetadataConflictAction.Error,
                It.IsAny<CancellationToken>()), dryRun ? Times.Never() : Times.Once());
            _lifecycleMetadataServiceMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_File_Conflict_DoesNotWaitForIngestion()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string path = Path.Combine(tempFolderContext.Path, "eol.json");
            File.WriteAllText(path, """
                {
                  "EolDate": "2026-01-01",
                  "EolDigests": [
                    { "Digest": "myacr.azurecr.io/repo@sha256:existing" }
                  ]
                }
                """);
            Mock<IMarImageIngestionReporter> ingestionReporterMock = new(MockBehavior.Strict);
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(), Mock.Of<IAcrContentClientFactory>(),
                ingestionReporter: ingestionReporterMock.Object);
            string existingDigest = $"{AcrName}/repo@sha256:existing";
            FileLifecycleMetadataOptions options = new()
            {
                EolDigestsListPath = path,
                WaitForIngestion = true
            };
            InvalidOperationException conflict = new("Conflicting EOL date.");
            _lifecycleMetadataServiceMock.Setup(o => o.AttachLifecycleMetadataAsync(
                existingDigest, new DateOnly(2026, 1, 1), false, options.OnConflict, It.IsAny<CancellationToken>()))
                .ThrowsAsync(conflict);

            InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
                () => command.AttachFromFileAsync(options, TestContext?.CancellationToken ?? default));

            exception.ShouldBeSameAs(conflict);
            ingestionReporterMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("append")]
        [DataRow("unknown")]
        public void AttachLifecycleMetadata_File_InvalidConflictAction_Rejected(string action)
        {
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(), Mock.Of<IAcrContentClientFactory>());

            var parseResult = command.GetCliCommand().Parse($"file eol.json --on-conflict {action}");

            parseResult.Errors.ShouldNotBeEmpty();
        }

        [TestMethod]
        [DataRow("""{"EolDigests":[{"Digest":"myacr.azurecr.io/repo@sha256:first"}]}""")]
        [DataRow("""{"EolDate":"2026-01-01","EolDigests":[{"Digest":""}]}""")]
        [DataRow("""{"EolDigests":[{"Digest":"myacr.azurecr.io/repo@sha256:first","EolDate":"2026-01-01"},{"Digest":"myacr.azurecr.io/repo@sha256:second"}]}""")]
        public async Task AttachLifecycleMetadata_File_MissingDigestOrDate_Throws(string json)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string path = Path.Combine(tempFolderContext.Path, "eol.json");
            File.WriteAllText(path, json);
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(), Mock.Of<IAcrContentClientFactory>());

            await Should.ThrowAsync<InvalidOperationException>(() => command.AttachFromFileAsync(
                new() { EolDigestsListPath = path }, TestContext?.CancellationToken ?? default));

            _lifecycleMetadataServiceMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_File_NullJson_Throws()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string path = Path.Combine(tempFolderContext.Path, "eol.json");
            File.WriteAllText(path, "null");
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(), Mock.Of<IAcrContentClientFactory>());

            await Should.ThrowAsync<JsonException>(() => command.AttachFromFileAsync(
                new() { EolDigestsListPath = path }, TestContext?.CancellationToken ?? default));

            _lifecycleMetadataServiceMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_File_IngestionFails_Throws()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string path = Path.Combine(tempFolderContext.Path, "eol.json");
            File.WriteAllText(path, """
                {"EolDate":"2026-01-01","EolDigests":[{"Digest":"myacr.azurecr.io/repo@sha256:first"}]}
                """);
            TimeoutException timeout = new("Annotation ingestion timed out.");
            Mock<IMarImageIngestionReporter> ingestionReporterMock = new();
            ingestionReporterMock.Setup(r => r.ReportImageStatusesAsync(
                It.IsAny<IServiceConnection>(), It.IsAny<IEnumerable<DigestInfo>>(),
                It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>(), null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(timeout);
            AttachLifecycleMetadataCommand command = InitializeCommand(
                Mock.Of<IAcrClientFactory>(), Mock.Of<IAcrContentClientFactory>(),
                ingestionReporter: ingestionReporterMock.Object);

            TimeoutException exception = await Should.ThrowAsync<TimeoutException>(() => command.AttachFromFileAsync(
                new() { EolDigestsListPath = path, WaitForIngestion = true }, TestContext?.CancellationToken ?? default));

            exception.ShouldBeSameAs(timeout);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_RepoRemoved()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);
            string repo2Image1DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    },
                    new RepoData
                    {
                        Repo = "repo2",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo2Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "newtag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo2", digest: "platformdigest201"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo2", digest: "imagedigest201")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove 'repo1' repo, with its images
            imageArtifactDetails.Repos.RemoveAt(0);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["1.0"])
                        ]),
                    CreateContainerRepository($"{DefaultRepoPrefix}repo2",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest201", tags: ["newtag"]),
                            CreateArtifactManifestProperties(digest: "imagedigest201", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        }),
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo2",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest201", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest201", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ImageRemoved()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2amd64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/amd64", tempFolderContext);
            string repo1Image2arm64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/arm64", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove second image from 'repo1' repo
            imageArtifactDetails.Repos[0].Images.RemoveAt(1);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-amd64"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ExcludeDigestsThatAreAlreadyAnnotated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2amd64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/amd64", tempFolderContext);
            string repo1Image2arm64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/arm64", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove second image from 'repo1' repo
            imageArtifactDetails.Repos[0].Images.RemoveAt(1);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            string armDigest = DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64");

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory,
                    // Already annotated, so it should be skipped.
                    annotatedDigests: [armDigest]);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-amd64"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_DockerfileInSeveralImages_OnlyOneUpdated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            },
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update image and platform digests in only one image that uses the shared Dockerfile
            imageArtifactDetails.Repos[0].Images[1].Manifest.Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102-updated");
            imageArtifactDetails.Repos[0].Images[1].Platforms[0].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102"),
                            CreateArtifactManifestProperties(digest: "imagedigest102-updated", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102-updated", new ManifestQueryResult(string.Empty, []) }
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest102"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ImageAndPlatformUpdated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "1.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update and image and platform digests
            imageArtifactDetails.Repos[0].Images[0].Manifest.Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101-updated");
            imageArtifactDetails.Repos[0].Images[0].Platforms[0].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101"),
                            CreateArtifactManifestProperties(digest: "platformdigest101-updated", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101"),
                            CreateArtifactManifestProperties(digest: "imagedigest101-updated", tags: ["1.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest101-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101-updated", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_JustOnePlatformUpdated()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag2"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update just one platform digest
            imageArtifactDetails.Repos[0].Images[0].Platforms[1].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["tag2"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102")
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_DoNotReturnAnnotationDigest()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);
            string repo1Image2DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os2", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2DockerfilePath,
                                        simpleTags:
                                        [
                                            "tag2"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "1.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Update just one platform digest
            imageArtifactDetails.Repos[0].Images[0].Platforms[1].Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-updated");

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102"),
                            CreateArtifactManifestProperties(digest: "platformdigest102-updated", tags: ["tag2"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                            CreateArtifactManifestProperties(digest: "annotationdigest"),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-updated", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                            // Define a subject field in this manifest to indicate it is a referrer, not an image manifest
                            { "annotationdigest", new ManifestQueryResult(string.Empty, new JsonObject { { "subject", "" } }) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102")
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_PlatformRemoved()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image2amd64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/amd64", tempFolderContext);
            string repo1Image2arm64DockerfilePath = DockerfileHelper.CreateDockerfile("2.0/runtime/arm64", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2amd64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-amd64")),
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image2arm64DockerfilePath,
                                        simpleTags:
                                        [
                                            "2.0"
                                        ],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest102-arm64"))
                                },
                                ProductVersion = "2.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags =
                                    [
                                        "2.0"
                                    ],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest102")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove one platform
            imageArtifactDetails.Repos[0].Images[0].Platforms.RemoveAt(1);

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest102-amd64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "platformdigest102-arm64", tags: ["2.0"]),
                            CreateArtifactManifestProperties(digest: "imagedigest102", tags: ["2.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { "platformdigest102-amd64", new ManifestQueryResult(string.Empty, []) },
                            { "platformdigest102-arm64", new ManifestQueryResult(string.Empty, []) },
                            { "imagedigest102", new ManifestQueryResult(string.Empty, []) },
                        })
                ]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest102-arm64"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_ManifestDeletedDuringEnumeration_Skipped()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repo1Image1DockerfilePath = DockerfileHelper.CreateDockerfile("1.0/runtime/os", tempFolderContext);

            ImageArtifactDetails imageArtifactDetails = new()
            {
                Repos =
                {
                    new RepoData
                    {
                        Repo = "repo1",
                        Images =
                        {
                            new ImageData
                            {
                                Platforms =
                                {
                                    Helpers.ImageInfoHelper.CreatePlatform(repo1Image1DockerfilePath,
                                        simpleTags: ["tag"],
                                        digest: DockerHelper.GetImageName(McrName, "repo1", digest: "platformdigest101"))
                                },
                                ProductVersion = "1.0",
                                Manifest = new ManifestData
                                {
                                    SharedTags = ["1.0"],
                                    Digest = DockerHelper.GetImageName(McrName, "repo1", digest: "imagedigest101")
                                }
                            }
                        }
                    }
                }
            };

            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Remove all images so everything in the registry is considered unsupported
            imageArtifactDetails.Repos.Clear();

            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(imageArtifactDetails));

            // Registry lists three manifests, but one will return 404 when fetched (simulating concurrent deletion)
            Mock<IAcrClient> registryClientMock = CreateAcrClientMock(
                [
                    CreateContainerRepository($"{DefaultRepoPrefix}repo1",
                        manifestProperties: [
                            CreateArtifactManifestProperties(digest: "platformdigest101", tags: ["tag"]),
                            CreateArtifactManifestProperties(digest: "deleteddigest", tags: ["old"]),
                            CreateArtifactManifestProperties(digest: "imagedigest101", tags: ["1.0"]),
                        ])
                ]);
            IAcrClientFactory registryClientFactory = CreateAcrClientFactory(
                AcrName, registryClientMock.Object);

            // Set up content client mock where "deleteddigest" throws a 404
            Mock<IAcrContentClient> contentClientMock = CreateAcrContentClientMock($"{DefaultRepoPrefix}repo1",
                imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                {
                    { "platformdigest101", new ManifestQueryResult(string.Empty, []) },
                    { "imagedigest101", new ManifestQueryResult(string.Empty, []) },
                });
            contentClientMock
                .Setup(o => o.GetManifestAsync("deleteddigest", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new RequestFailedException(404, "manifest not found"));

            IAcrContentClientFactory registryContentClientFactory = CreateAcrContentClientFactory(AcrName,
                [contentClientMock]);

            AttachLifecycleMetadataCommand command =
                InitializeCommand(
                    registryClientFactory,
                    registryContentClientFactory);
            await command.AttachToUnsupportedAsync(
                CreatePublishedOptions(oldImageInfoPath, newImageInfoPath),
                TestContext?.CancellationToken ?? default);

            string[] expectedDigests =
                [
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "imagedigest101"),
                    DockerHelper.GetImageName(AcrName, $"{DefaultRepoPrefix}repo1", digest: "platformdigest101"),
                ];

            _annotatedDigests.ShouldBe(expectedDigests, ignoreOrder: true);
        }

        [TestMethod]
        [DataRow("file", true, true, false, DisplayName = "File waits only for new artifacts")]
        [DataRow("all", true, true, false, DisplayName = "Registry command wires ingestion options")]
        [DataRow("file", false, true, false, DisplayName = "No wait when nothing was created")]
        [DataRow("file", true, false, false, DisplayName = "No wait without opt-in")]
        [DataRow("file", true, true, true, DisplayName = "No wait during dry run")]
        public async Task AttachLifecycleMetadata_WaitsOnlyForNewArtifacts(
            string subcommand, bool createNew, bool wait, bool dryRun)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string path = Path.Combine(tempFolderContext.Path, "eol.json");
            File.WriteAllText(path, """
                {
                  "EolDate": "2026-01-01",
                  "EolDigests": [
                    { "Digest": "myacr.azurecr.io/repo@sha256:new" },
                    { "Digest": "myacr.azurecr.io/repo@sha256:matching" },
                    { "Digest": "myacr.azurecr.io/repo@sha256:conflicting" }
                  ]
                }
                """);
            Mock<IMarImageIngestionReporter> ingestionReporterMock = new();
            AttachLifecycleMetadataCommand command = InitializeCommand(
                CreateAcrClientFactory(AcrName, CreateAcrClientMock(
                    [CreateContainerRepository("repo", manifestProperties:
                        [
                            CreateArtifactManifestProperties(digest: "sha256:new"),
                            CreateArtifactManifestProperties(digest: "sha256:matching"),
                            CreateArtifactManifestProperties(digest: "sha256:conflicting")
                        ])]).Object),
                CreateAcrContentClientFactory(AcrName,
                    [CreateAcrContentClientMock("repo",
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            ["sha256:new"] = new(string.Empty, []),
                            ["sha256:matching"] = new(string.Empty, []),
                            ["sha256:conflicting"] = new(string.Empty, [])
                        })]),
                ingestionReporter: ingestionReporterMock.Object);

            foreach ((string digest, LifecycleMetadataAttachmentStatus status) in new[]
            {
                ("new", createNew ? LifecycleMetadataAttachmentStatus.Attached : LifecycleMetadataAttachmentStatus.AlreadyMatching),
                ("matching", LifecycleMetadataAttachmentStatus.AlreadyMatching),
                ("conflicting", LifecycleMetadataAttachmentStatus.ConflictSkipped)
            })
            {
                string reference = $"{AcrName}/repo@sha256:{digest}";
                _lifecycleMetadataServiceMock.Setup(o => o.AttachLifecycleMetadataAsync(
                    reference, It.IsAny<DateOnly>(), false, LifecycleMetadataConflictAction.Skip,
                    It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new LifecycleMetadataAttachmentResult(
                        status, LifecycleArtifactHelper.CreateLifecycleArtifact($"{reference}-lifecycle")));
            }

            string arguments = subcommand == "file" ? $"file \"{path}\"" : $"all {AcrName} public/";
            var parseResult = command.GetCliCommand().Parse(
                $"{arguments} --no-version-logging --on-conflict skip"
                + $" --timeout 00:07:00 --requery-delay 00:00:03 --mar-service-connection tenant:client:connection"
                + (wait ? " --wait-for-ingestion" : "")
                + (dryRun ? " --dry-run" : ""));
            parseResult.Errors.ShouldBeEmpty();

            int exitCode = await parseResult.InvokeAsync(cancellationToken: TestContext?.CancellationToken ?? default);

            exitCode.ShouldBe(0);

            ingestionReporterMock.Verify(r => r.ReportImageStatusesAsync(
                It.Is<IServiceConnection>(connection =>
                    connection.Id == "connection" && connection.TenantId == "tenant" && connection.ClientId == "client"),
                It.Is<IEnumerable<DigestInfo>>(digests =>
                    digests.Single().Digest == "sha256:new-lifecycle" && digests.Single().Repo == "repo"),
                TimeSpan.FromMinutes(7),
                TimeSpan.FromSeconds(3),
                null,
                It.IsAny<CancellationToken>()),
                createNew && wait && !dryRun ? Times.Once() : Times.Never());
            ingestionReporterMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("", false, LifecycleMetadataConflictAction.Error, LifecycleMetadataAttachmentStatus.Attached)]
        [DataRow("--mark-as-internal", true, LifecycleMetadataConflictAction.Error, LifecycleMetadataAttachmentStatus.Attached)]
        [DataRow("--mark-as-internal false", false, LifecycleMetadataConflictAction.Error, LifecycleMetadataAttachmentStatus.Attached)]
        [DataRow("", false, LifecycleMetadataConflictAction.Skip, LifecycleMetadataAttachmentStatus.Attached)]
        [DataRow("", false, LifecycleMetadataConflictAction.Error, LifecycleMetadataAttachmentStatus.AlreadyMatching)]
        [DataRow("", false, LifecycleMetadataConflictAction.Skip, LifecycleMetadataAttachmentStatus.ConflictSkipped)]
        public async Task AttachLifecycleMetadata_Published_WaitsForCreatedAnnotations(
            string internalOption, bool markAsInternal, LifecycleMetadataConflictAction onConflict,
            LifecycleMetadataAttachmentStatus status)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            // The repo is only in the old image info, so all of its images are unsupported.
            string oldImageInfoPath = Path.Combine(tempFolderContext.Path, "old-image-info.json");
            File.WriteAllText(oldImageInfoPath, JsonHelper.SerializeObject(
                new ImageArtifactDetails { Repos = { new RepoData { Repo = "repo1" } } }));
            string newImageInfoPath = Path.Combine(tempFolderContext.Path, "new-image-info.json");
            File.WriteAllText(newImageInfoPath, JsonHelper.SerializeObject(new ImageArtifactDetails()));

            string repo = $"{DefaultRepoPrefix}repo1";
            Mock<IMarImageIngestionReporter> ingestionReporterMock = new();
            AttachLifecycleMetadataCommand command = InitializeCommand(
                CreateAcrClientFactory(AcrName, CreateAcrClientMock(
                    [CreateContainerRepository(repo, manifestProperties: [CreateArtifactManifestProperties(digest: "sha256:new")])]).Object),
                CreateSingleImageContentClientFactory(repo, "sha256:new"),
                ingestionReporter: ingestionReporterMock.Object);

            string newDigest = DockerHelper.GetImageName(AcrName, repo, digest: "sha256:new");
            _lifecycleMetadataServiceMock.Setup(o => o.AttachLifecycleMetadataAsync(
                newDigest, _globalDate, markAsInternal, onConflict, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LifecycleMetadataAttachmentResult(
                    status, LifecycleArtifactHelper.CreateLifecycleArtifact($"{newDigest}-lifecycle")));

            var parseResult = command.GetCliCommand().Parse(
                $"published {AcrName} {DefaultRepoPrefix} \"{oldImageInfoPath}\" \"{newImageInfoPath}\""
                + $" --wait-for-ingestion --no-version-logging {internalOption} --on-conflict {onConflict}");
            parseResult.Errors.ShouldBeEmpty();
            (await parseResult.InvokeAsync(cancellationToken: TestContext?.CancellationToken ?? default)).ShouldBe(0);

            _lifecycleMetadataServiceMock.Verify(o => o.AttachLifecycleMetadataAsync(
                newDigest, _globalDate, markAsInternal, onConflict, It.IsAny<CancellationToken>()));
            ingestionReporterMock.Verify(r => r.ReportImageStatusesAsync(
                It.IsAny<IServiceConnection>(),
                It.Is<IEnumerable<DigestInfo>>(digests =>
                    digests.Single().Digest == "sha256:new-lifecycle" && digests.Single().Repo == repo),
                TimeSpan.FromMinutes(20),
                TimeSpan.FromSeconds(10),
                null,
                It.IsAny<CancellationToken>()),
                status == LifecycleMetadataAttachmentStatus.Attached ? Times.Once() : Times.Never());
            ingestionReporterMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow("", false, LifecycleMetadataConflictAction.Error)]
        [DataRow("--mark-as-internal", true, LifecycleMetadataConflictAction.Error)]
        [DataRow("--mark-as-internal false", false, LifecycleMetadataConflictAction.Error)]
        [DataRow("--mark-as-internal", true, LifecycleMetadataConflictAction.Skip)]
        public async Task AttachLifecycleMetadata_All_UsesInternalOption(
            string internalOption, bool markAsInternal, LifecycleMetadataConflictAction onConflict)
        {
            AttachLifecycleMetadataCommand command = InitializeCommand(
                CreateAcrClientFactory(AcrName, CreateAcrClientMock(
                    [
                        CreateContainerRepository("repo1",
                            manifestProperties: [
                                CreateArtifactManifestProperties(digest: "sha256:new"),
                                CreateArtifactManifestProperties(digest: "sha256:existing"),
                            ])
                    ]).Object),
                CreateAcrContentClientFactory(AcrName,
                    [
                        CreateAcrContentClientMock("repo1",
                            imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                            {
                                { "sha256:new", new ManifestQueryResult(string.Empty, []) },
                                { "sha256:existing", new ManifestQueryResult(string.Empty, []) },
                            })
                    ]));

            string existingDigest = DockerHelper.GetImageName(AcrName, "repo1", digest: "sha256:existing");
            _lifecycleMetadataServiceMock
                .Setup(o => o.AttachLifecycleMetadataAsync(
                    existingDigest, _globalDate, true, onConflict, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LifecycleMetadataAttachmentResult(
                    onConflict == LifecycleMetadataConflictAction.Skip
                        ? LifecycleMetadataAttachmentStatus.ConflictSkipped
                        : LifecycleMetadataAttachmentStatus.AlreadyMatching,
                    LifecycleArtifactHelper.CreateLifecycleArtifact($"{existingDigest}-lifecycle", _globalDate)));

            var parseResult = command.GetCliCommand().Parse(
                $"all {AcrName} {DefaultRepoPrefix} --no-version-logging {internalOption} --on-conflict {onConflict}");
            parseResult.Errors.ShouldBeEmpty();
            (await parseResult.InvokeAsync(cancellationToken: TestContext?.CancellationToken ?? default)).ShouldBe(0);

            string newDigest = DockerHelper.GetImageName(AcrName, "repo1", digest: "sha256:new");
            _annotatedDigests.ShouldBe(
                markAsInternal ? [newDigest] : [newDigest, existingDigest], ignoreOrder: true);
            _lifecycleMetadataServiceMock.Verify(o => o.AttachLifecycleMetadataAsync(
                newDigest, _globalDate, markAsInternal, onConflict, It.IsAny<CancellationToken>()));
            _lifecycleMetadataServiceMock.Verify(o => o.AttachLifecycleMetadataAsync(
                existingDigest, _globalDate, markAsInternal, onConflict, It.IsAny<CancellationToken>()));
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_All_DryRun_SkipsRegistryAccess()
        {
            Mock<IAcrClientFactory> registryClientFactory = new(MockBehavior.Strict);
            Mock<IAcrContentClientFactory> registryContentClientFactory = new(MockBehavior.Strict);
            AttachLifecycleMetadataCommand command = InitializeCommand(
                registryClientFactory.Object, registryContentClientFactory.Object);

            RegistryLifecycleMetadataOptions options = new()
            {
                RegistryOptions = new() { Registry = AcrName },
                IsDryRun = true
            };

            await command.AttachToAllAsync(options, TestContext?.CancellationToken ?? default);

            registryClientFactory.VerifyNoOtherCalls();
            registryContentClientFactory.VerifyNoOtherCalls();
            _lifecycleMetadataServiceMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task AttachLifecycleMetadata_AnnotationFails_Throws()
        {
            AttachLifecycleMetadataCommand command = InitializeCommand(
                CreateAcrClientFactory(AcrName, CreateAcrClientMock(
                    [CreateContainerRepository("repo1", manifestProperties: [CreateArtifactManifestProperties(digest: "sha256:a")])]).Object),
                CreateSingleImageContentClientFactory("repo1", "sha256:a"),
                annotationSucceeds: false);

            RegistryLifecycleMetadataOptions options = new()
            {
                RegistryOptions = new() { Registry = AcrName }
            };

            await Should.ThrowAsync<InvalidOperationException>(
                () => command.AttachToAllAsync(options, TestContext?.CancellationToken ?? default));
        }

        private AttachLifecycleMetadataCommand InitializeCommand(
            IAcrClientFactory registryClientFactory,
            IAcrContentClientFactory registryContentClientFactory,
            IEnumerable<string> annotatedDigests = null,
            bool annotationSucceeds = true,
            IMarImageIngestionReporter ingestionReporter = null)
        {
            _lifecycleMetadataServiceMock
                .Setup(o => o.AttachLifecycleMetadataAsync(
                    It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<bool>(),
                    It.IsAny<LifecycleMetadataConflictAction>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string digest, DateOnly date, bool _, LifecycleMetadataConflictAction _, CancellationToken _) =>
                {
                    if (!annotationSucceeds)
                    {
                        throw new InvalidOperationException($"Failed to attach lifecycle metadata to '{digest}'.");
                    }

                    _annotatedDigests.Add(digest);
                    return new LifecycleMetadataAttachmentResult(
                        LifecycleMetadataAttachmentStatus.Attached,
                        LifecycleArtifactHelper.CreateLifecycleArtifact($"{digest}-lifecycle", date));
                });

            foreach (string digest in annotatedDigests ?? [])
            {
                _lifecycleMetadataServiceMock
                    .Setup(o => o.AttachLifecycleMetadataAsync(
                        digest, _globalDate, false, It.IsAny<LifecycleMetadataConflictAction>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new LifecycleMetadataAttachmentResult(
                        LifecycleMetadataAttachmentStatus.AlreadyMatching,
                        LifecycleArtifactHelper.CreateLifecycleArtifact($"{digest}-lifecycle", _globalDate)));
            }

            return new AttachLifecycleMetadataCommand(
                logger: Mock.Of<ILogger<AttachLifecycleMetadataCommand>>(),
                acrClientFactory: registryClientFactory,
                acrContentClientFactory: registryContentClientFactory,
                lifecycleMetadataService: _lifecycleMetadataServiceMock.Object,
                ingestionReporter: ingestionReporter ?? Mock.Of<IMarImageIngestionReporter>(),
                // Image info paths are absolute, so the artifact root isn't used.
                artifactService: TestHelper.CreateArtifactService(Path.GetTempPath()));
        }

        private static UnsupportedLifecycleMetadataOptions CreatePublishedOptions(
            string oldImageInfoPath,
            string newImageInfoPath) =>
            new()
            {
                OldImageInfoPath = oldImageInfoPath,
                NewImageInfoPath = newImageInfoPath,
                RegistryOptions = new() { RepoPrefix = DefaultRepoPrefix, Registry = AcrName }
            };

        private static IAcrContentClientFactory CreateSingleImageContentClientFactory(string repo, string digest) =>
            CreateAcrContentClientFactory(AcrName,
                [
                    CreateAcrContentClientMock(repo,
                        imageNameToQueryResultsMapping: new Dictionary<string, ManifestQueryResult>
                        {
                            { digest, new ManifestQueryResult(string.Empty, []) },
                        })
                ]);
    }
}
