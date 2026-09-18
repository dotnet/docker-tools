// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.DockerfileHelper;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class PublishMcrDocsCommandTests
    {
        public TestContext? TestContext { get; set; }

        private const string ProductFamilyReadmePath = "ProductFamilyReadme.md";
        private const string RepoReadmePath = "RepoReadme.md";
        private const string TagsYamlPath = "tags.yml";
        private const string DefaultReadme = "Default Readme Contents\n# Full Tag Listing\n<!--End of generated tags-->\n";
        private const string ReadmeTemplatePath = "Readme.Template.md";
        private const string AboutRepoTemplatePath = "About.repo.Template.md";
        private const string AboutRepoTemplate =
@"Referenced Template Content";
        private const string ReadmeTemplate =
@"About {{if IS_PRODUCT_FAMILY:Product Family^else:{{SHORT_REPO}}}}
{{if !IS_PRODUCT_FAMILY:{{InsertTemplate(join(filter([""About"", SHORT_REPO, ""Template"", ""md""], len), "".""))}}}}";

        [TestMethod]
        public async Task ExcludeProductFamilyReadme()
        {
            Mock<IGitHubClient> gitHubClientMock = CreateGitHubClientMock();
            IGitHubClientFactory gitHubClientFactory = CreateGitHubClientFactory(gitHubClientMock);

            PublishMcrDocsCommand command = new(TestHelper.CreateManifestJsonService(), Mock.Of<IGitService>(), gitHubClientFactory, Mock.Of<ILogger<PublishMcrDocsCommand>>());

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            DockerfileHelper.CreateFile(ProductFamilyReadmePath, tempFolderContext, DefaultReadme);
            DockerfileHelper.CreateFile(RepoReadmePath, tempFolderContext, DefaultReadme);
            DockerfileHelper.CreateFile(AboutRepoTemplatePath, tempFolderContext, AboutRepoTemplate);
            DockerfileHelper.CreateFile(ReadmeTemplatePath, tempFolderContext, ReadmeTemplate);

            string tagsMetadataTemplatePath = CreateMcrTagsMetadataTemplateFile(tempFolderContext);

            Repo repo;
            Manifest manifest = CreateManifest(
                repo = CreateRepo("dotnet/repo", new Image[]
                {
                    CreateImage(
                        CreatePlatform(CreateDockerfile("1.0/runtime/linux", tempFolderContext), new string[] { "tag" }))
                }, RepoReadmePath, ReadmeTemplatePath, Path.GetFileName(tagsMetadataTemplatePath)));
            manifest.Registry = "mcr.microsoft.com";
            manifest.Readme = new(ProductFamilyReadmePath, ReadmeTemplatePath);
            repo.Id = "repo";

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            command.Options.Manifest = manifestPath;
            command.Options.ExcludeProductFamilyReadme = true;
            command.LoadManifest();

            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            // Verify published file list does not contain ProductFamilyReadmePath
            gitHubClientMock
                .Verify(o =>
                    o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(),
                        It.Is<GitObject[]>(objs =>
                            objs.Length == 2 &&
                            Path.GetFileName(objs[0].Path) == RepoReadmePath &&
                            Path.GetFileName(objs[1].Path) == TagsYamlPath)));
        }

        [TestMethod]
        public async Task RootPathOption()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string readme1 = RepoReadmePath;
            string readme2 = Path.Combine("dir", RepoReadmePath);
            const string ReadmeContents = "Readme Contents";
            const string readme2Content = ReadmeContents + "-readme2";

            CreateFile(ProductFamilyReadmePath, tempFolderContext, DefaultReadme);
            CreateFile(readme1, tempFolderContext, ReadmeContents);
            CreateFile(readme2, tempFolderContext, readme2Content);
            CreateFile(AboutRepoTemplatePath, tempFolderContext, AboutRepoTemplate);
            CreateFile(ReadmeTemplatePath, tempFolderContext, ReadmeTemplate);

            string tagsMetadataTemplatePath = CreateMcrTagsMetadataTemplateFile(tempFolderContext);

            Manifest manifest = CreateManifest(
                new Repo
                {
                    Name = "dotnet/repo",
                    Id = "repo",
                    Images = [
                        CreateImage(
                            CreatePlatform(CreateDockerfile("1.0/runtime/linux", tempFolderContext), ["tag"]))
                    ],
                    McrTagsMetadataTemplate = Path.GetFileName(tagsMetadataTemplatePath),
                    Readmes = [
                        new Readme(readme1, ReadmeTemplatePath),
                        new Readme(readme2, ReadmeTemplatePath)
                    ]
                });

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            Mock<IGitHubClient> gitHubClientMock = CreateGitHubClientMock();
            IGitHubClientFactory gitHubClientFactory = CreateGitHubClientFactory(gitHubClientMock);

            PublishMcrDocsCommand command = new(TestHelper.CreateManifestJsonService(), Mock.Of<IGitService>(), gitHubClientFactory, Mock.Of<ILogger<PublishMcrDocsCommand>>());
            command.Options.Manifest = manifestPath;
            command.Options.ExcludeProductFamilyReadme = true;
            command.Options.RootPath = Path.Combine(tempFolderContext.Path, "dir");
            command.LoadManifest();

            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            gitHubClientMock
                .Verify(o =>
                    o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(),
                        It.Is<GitObject[]>(objs =>
                            objs.Length == 2 &&
                            Path.GetFileName(objs[0].Path) == RepoReadmePath &&
                            Path.GetFileName(objs[1].Path) == TagsYamlPath &&
                            objs[0].Content == readme2Content)));

            gitHubClientMock.Verify(o =>
                o.GetGitHubFileContentsAsync(It.IsAny<string>(), It.IsAny<GitHubBranch>()));
            gitHubClientMock.Verify(o =>
                o.GetReferenceAsync(It.IsAny<GitHubProject>(), It.IsAny<string>()));
            gitHubClientMock.Verify(o =>
                o.PostCommitAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()));
            gitHubClientMock.Verify(o =>
                o.PatchReferenceAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()));
            gitHubClientMock.Verify(o => o.Dispose());

            gitHubClientMock.VerifyNoOtherCalls();
        }

        [TestMethod]
        public async Task RoutesReadmesToEachProductRepo()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string dotnetReadmePath = Path.Combine("dotnet", "README.portal.md");
            string aspireReadmePath = Path.Combine("aspire", "README.portal.md");
            const string DotnetTagsMetadataPath = "runtime-tags.yml";
            const string AspireTagsMetadataPath = "aspire-dashboard-tags.yml";

            CreateFile(dotnetReadmePath, tempFolderContext, DefaultReadme);
            CreateFile(aspireReadmePath, tempFolderContext, DefaultReadme);
            CreateFile(ReadmeTemplatePath, tempFolderContext, ReadmeTemplate);
            CreateFile(
                DotnetTagsMetadataPath,
                tempFolderContext,
                "$(McrTagsYmlRepo:runtime)\n$(McrTagsYmlTagGroup:runtime-tag)");
            CreateFile(
                AspireTagsMetadataPath,
                tempFolderContext,
                "$(McrTagsYmlRepo:aspire-dashboard)\n$(McrTagsYmlTagGroup:aspire-tag)");

            Repo runtimeRepo = CreateRepo(
                "dotnet/nightly/runtime",
                [
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/runtime/linux", tempFolderContext),
                            ["runtime-tag"]))
                ],
                dotnetReadmePath,
                ReadmeTemplatePath,
                DotnetTagsMetadataPath);
            runtimeRepo.Id = "runtime";

            Repo aspireRepo = CreateRepo(
                "aspire/nightly/dashboard",
                [
                    CreateImage(
                        CreatePlatform(
                            CreateDockerfile("1.0/dashboard/linux", tempFolderContext),
                            ["aspire-tag"]))
                ],
                aspireReadmePath,
                ReadmeTemplatePath,
                AspireTagsMetadataPath);
            aspireRepo.Id = "aspire-dashboard";

            Manifest manifest = CreateManifest(runtimeRepo, aspireRepo);
            manifest.Registry = "mcr.microsoft.com";

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            Mock<IGitHubClient> gitHubClientMock = CreateGitHubClientMock();
            PublishMcrDocsCommand command = new(
                TestHelper.CreateManifestJsonService(),
                Mock.Of<IGitService>(),
                CreateGitHubClientFactory(gitHubClientMock),
                Mock.Of<ILogger<PublishMcrDocsCommand>>());
            command.Options.Manifest = manifestPath;
            command.Options.ExcludeProductFamilyReadme = true;
            command.Options.GitOptions.Path = "teams";
            command.LoadManifest();

            await command.ExecuteAsync(TestContext?.CancellationToken ?? default);

            gitHubClientMock.Verify(o =>
                o.PostTreeAsync(
                    It.IsAny<GitHubProject>(),
                    It.IsAny<string>(),
                    It.Is<GitObject[]>(objects =>
                        objects.Length == 4 &&
                        objects.Any(obj => obj.Path == "teams/dotnet/nightly/README.portal.md") &&
                        objects.Any(obj => obj.Path == "teams/dotnet/nightly/runtime-tags.yml") &&
                        objects.Any(obj => obj.Path == "teams/aspire/nightly/README.portal.md") &&
                        objects.Any(obj => obj.Path == "teams/aspire/nightly/aspire-dashboard-tags.yml"))));
        }

        [TestMethod]
        public async Task DuplicateFilename()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();

            string readme1 = RepoReadmePath;
            string readme2 = Path.Combine("dir", RepoReadmePath);
            const string ReadmeContents = "Readme Contents";
            const string readme2Content = ReadmeContents + "-readme2";

            CreateFile(ProductFamilyReadmePath, tempFolderContext, DefaultReadme);
            CreateFile(readme1, tempFolderContext, ReadmeContents);
            CreateFile(readme2, tempFolderContext, readme2Content);
            CreateFile(AboutRepoTemplatePath, tempFolderContext, AboutRepoTemplate);
            CreateFile(ReadmeTemplatePath, tempFolderContext, ReadmeTemplate);

            string tagsMetadataTemplatePath = CreateMcrTagsMetadataTemplateFile(tempFolderContext);

            Manifest manifest = CreateManifest(
                new Repo
                {
                    Name = "dotnet/repo",
                    Id = "repo",
                    Images = [
                        CreateImage(
                            CreatePlatform(CreateDockerfile("1.0/runtime/linux", tempFolderContext), ["tag"]))
                    ],
                    McrTagsMetadataTemplate = Path.GetFileName(tagsMetadataTemplatePath),
                    Readmes = [
                        new Readme(readme1, ReadmeTemplatePath),
                        new Readme(readme2, ReadmeTemplatePath)
                    ]
                });

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            Mock<IGitHubClient> gitHubClientMock = CreateGitHubClientMock();
            IGitHubClientFactory gitHubClientFactory = CreateGitHubClientFactory(gitHubClientMock);

            PublishMcrDocsCommand command = new(TestHelper.CreateManifestJsonService(), Mock.Of<IGitService>(), gitHubClientFactory, Mock.Of<ILogger<PublishMcrDocsCommand>>());
            command.Options.Manifest = manifestPath;
            command.Options.ExcludeProductFamilyReadme = true;
            command.LoadManifest();

            await Should.ThrowAsync<ValidationException>(() => command.ExecuteAsync(TestContext?.CancellationToken ?? default));
        }

        private static string CreateMcrTagsMetadataTemplateFile(TempFolderContext tempFolderContext)
        {
            StringBuilder tagsMetadataTemplateBuilder = new();
            tagsMetadataTemplateBuilder.AppendLine($"$(McrTagsYmlRepo:repo)");
            tagsMetadataTemplateBuilder.Append($"$(McrTagsYmlTagGroup:tag)");
            string tagsMetadataTemplatePath = Path.Combine(tempFolderContext.Path, TagsYamlPath);
            File.WriteAllText(tagsMetadataTemplatePath, tagsMetadataTemplateBuilder.ToString());
            return tagsMetadataTemplatePath;
        }

        private static Mock<IGitHubClient> CreateGitHubClientMock()
        {
            Mock<IGitHubClient> gitHubClientMock = new();
            gitHubClientMock
                .Setup(o => o.GetReferenceAsync(It.IsAny<GitHubProject>(), It.IsAny<string>()))
                .ReturnsAsync(new GitReference
                {
                    Object = new GitReferenceObject()
                });

            gitHubClientMock
                .Setup(o => o.PostTreeAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<GitObject[]>()))
                .ReturnsAsync(new GitTree());

            gitHubClientMock
                .Setup(o => o.PostCommitAsync(It.IsAny<GitHubProject>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .ReturnsAsync(new GitCommit());

            return gitHubClientMock;
        }

        private static IGitHubClientFactory CreateGitHubClientFactory(Mock<IGitHubClient> gitHubClientMock)
        {
            Mock<IGitHubClientFactory> gitHubClientFactoryMock = new();
            gitHubClientFactoryMock
                            .Setup(o => o.GetClientAsync(It.IsAny<GitOptions>(), false, It.IsAny<CancellationToken>()))
                            .ReturnsAsync(gitHubClientMock.Object);
            return gitHubClientFactoryMock.Object;
        }
    }
}
