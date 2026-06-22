// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Automation;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
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
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            string repoRoot = Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, "repo-root")).FullName;
            (Mock<IRepoHost> repoHostMock, IRepoHostFactory repoHostFactory) =
                CreateRepoHostMock(repoRoot);

            PublishMcrDocsCommand command = new(TestHelper.CreateManifestJsonService(), Mock.Of<IGitService>(), repoHostFactory, Mock.Of<ILogger<PublishMcrDocsCommand>>());

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

            await command.ExecuteAsync();

            // Verify published file list does not contain ProductFamilyReadmePath
            string[] publishedFiles = GetPublishedFileNames(repoRoot);
            publishedFiles.ShouldBe(new[] { RepoReadmePath, TagsYamlPath }, ignoreOrder: true);
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

            string repoRoot = Directory.CreateDirectory(Path.Combine(tempFolderContext.Path, "repo-root")).FullName;
            (Mock<IRepoHost> repoHostMock, IRepoHostFactory repoHostFactory) =
                CreateRepoHostMock(repoRoot);

            PublishMcrDocsCommand command = new(TestHelper.CreateManifestJsonService(), Mock.Of<IGitService>(), repoHostFactory, Mock.Of<ILogger<PublishMcrDocsCommand>>());
            command.Options.Manifest = manifestPath;
            command.Options.ExcludeProductFamilyReadme = true;
            command.Options.RootPath = Path.Combine(tempFolderContext.Path, "dir");
            command.LoadManifest();

            await command.ExecuteAsync();

            // Only the readme under RootPath is published, with its content intact.
            string[] publishedFiles = GetPublishedFileNames(repoRoot);
            publishedFiles.ShouldBe(new[] { RepoReadmePath, TagsYamlPath }, ignoreOrder: true);

            string publishedReadme = Directory.GetFiles(repoRoot, RepoReadmePath, SearchOption.AllDirectories)[0];
            File.ReadAllText(publishedReadme).ShouldBe(readme2Content);

            repoHostMock.Verify(o =>
                o.EnsureBranchContentAsync(It.IsAny<BranchSpec>(), It.IsAny<CancellationToken>()),
                Times.Once);
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

            (_, IRepoHostFactory repoHostFactory) = CreateRepoHostMock(tempFolderContext.Path);

            PublishMcrDocsCommand command = new(TestHelper.CreateManifestJsonService(), Mock.Of<IGitService>(), repoHostFactory, Mock.Of<ILogger<PublishMcrDocsCommand>>());
            command.Options.Manifest = manifestPath;
            command.Options.ExcludeProductFamilyReadme = true;
            command.LoadManifest();

            await Should.ThrowAsync<ValidationException>(() => command.ExecuteAsync());
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

        /// <summary>
        /// Creates an <see cref="IRepoHost"/> mock whose EnsureBranchContentAsync invokes the
        /// changes against <paramref name="repoRoot"/>, simulating a local clone.
        /// </summary>
        private static (Mock<IRepoHost> Host, IRepoHostFactory Factory) CreateRepoHostMock(string repoRoot)
        {
            Mock<IRepoHost> repoHostMock = new();
            repoHostMock
                .Setup(o => o.EnsureBranchContentAsync(It.IsAny<BranchSpec>(), It.IsAny<CancellationToken>()))
                .Returns(async (BranchSpec spec, CancellationToken _) =>
                {
                    TestGitContext context = new(repoRoot);
                    await spec.Apply(context, CancellationToken.None);
                    return new BranchResult
                    {
                        Outcome = BranchOutcome.Updated,
                        Commits = context.Commits,
                    };
                });

            Mock<IRepoHostFactory> repoHostFactoryMock = new();
            repoHostFactoryMock
                .Setup(o => o.CreateRepoHostAsync(It.IsAny<GitOptions>(), false))
                .ReturnsAsync(repoHostMock.Object);

            return (repoHostMock, repoHostFactoryMock.Object);
        }

        private static string[] GetPublishedFileNames(string repoRoot) =>
            [.. Directory.GetFiles(repoRoot, "*", SearchOption.AllDirectories).Select(file => Path.GetFileName(file))];

        private sealed class TestGitContext(string directory) : IGitContext
        {
            private readonly List<GitCommit> _commits = [];

            public string Directory { get; } = directory;

            public IReadOnlyList<GitCommit> Commits => _commits;

            public Task<GitCommit?> CommitAsync(string message, CancellationToken cancellationToken = default)
            {
                GitCommit commit = new("commitSha", "Test Author", "test@example.com", message);
                _commits.Add(commit);
                return Task.FromResult<GitCommit?>(commit);
            }
        }
    }
}
