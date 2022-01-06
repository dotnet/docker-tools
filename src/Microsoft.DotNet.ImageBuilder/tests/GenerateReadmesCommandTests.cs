// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Moq;
using Newtonsoft.Json;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    public class GenerateReadmesCommandTests
    {
        private const string ProductFamilyReadmePath = "ProductFamilyReadme.md";
        private const string RepoReadmePath = "RepoReadme.md";
        private const string DefaultReadme = "Default Readme Contents";
        private const string ReadmeTemplatePath = "Readme.Template.md";
        private const string AboutRepoTemplatePath = "About.repo.Template.md";
        private const string AboutRepoTemplate =
@"Referenced Template Content";
        private const string ReadmeTemplate =
@"About {{if IS_PRODUCT_FAMILY:Product Family^else:{{SHORT_REPO}}}}
{{if !IS_PRODUCT_FAMILY:{{InsertTemplate(join(filter([""About"", SHORT_REPO, ""Template"", ""md""], len), "".""))}}}}";
        private const string ExpectedProductFamilyReadme =
@"About Product Family
";
        private const string ExpectedRepoReadme =
@"About repo
Referenced Template Content";

        private readonly Exception _exitException = new Exception();
        private Mock<IEnvironmentService> _environmentServiceMock;

        public GenerateReadmesCommandTests()
        {
        }

        [Fact]
        public async Task GenerateReadmesCommand_Canonical()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateReadmesCommand command = InitializeCommand(tempFolderContext);

            await command.ExecuteAsync();

            string generatedReadme = File.ReadAllText(Path.Combine(tempFolderContext.Path, ProductFamilyReadmePath));
            Assert.Equal(ExpectedProductFamilyReadme.NormalizeLineEndings(generatedReadme), generatedReadme);

            generatedReadme = File.ReadAllText(Path.Combine(tempFolderContext.Path, RepoReadmePath));
            Assert.Equal(ExpectedRepoReadme.NormalizeLineEndings(generatedReadme), generatedReadme);
        }

        [Fact]
        public async Task GenerateReadmesCommand_TemplateArgs()
        {
            const string readmeTemplate =
@"Hello World
{{InsertTemplate(""template-with-args.md"", [ ""my-arg"": 123 ])}}";

            const string templateWithArgs =
@"ABC-{{ARGS[""my-arg""]}}";

            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            DockerfileHelper.CreateFile("template-with-args.md", tempFolderContext, templateWithArgs);
            GenerateReadmesCommand command = InitializeCommand(tempFolderContext, readmeTemplate);

            await command.ExecuteAsync();

            string generatedReadme = File.ReadAllText(Path.Combine(tempFolderContext.Path, ProductFamilyReadmePath));
            string expectedReadme =
@"Hello World
ABC-123";
            Assert.Equal(expectedReadme, generatedReadme);
        }

        [Fact]
        public async Task GenerateReadmesCommand_InvalidTemplate()
        {
            string template = "about{{if:}}";
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateReadmesCommand command = InitializeCommand(tempFolderContext, template);

            Exception actualException = await Assert.ThrowsAsync<Exception>(command.ExecuteAsync);

            Assert.Same(_exitException, actualException);
            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Once);
        }

        [Fact]
        public async Task GenerateReadmesCommand_MissingTemplate()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateReadmesCommand command = InitializeCommand(tempFolderContext, null, allowOptionalTemplates: false);

            await Assert.ThrowsAsync<InvalidOperationException>(command.ExecuteAsync);
        }

        [Fact]
        public async Task GenerateReadmesCommand_Validate_UpToDate()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateReadmesCommand command = InitializeCommand(
                tempFolderContext, productFamilyReadme: ExpectedProductFamilyReadme, repoReadme: ExpectedRepoReadme, validate: true);

            await command.ExecuteAsync();

            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GenerateReadmesCommand_Validate_OutOfSync()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateReadmesCommand command = InitializeCommand(tempFolderContext, validate: true);

            Exception actualException = await Assert.ThrowsAsync<Exception>(command.ExecuteAsync);

            Assert.Same(_exitException, actualException);
            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Once);
            // Validate readmes remain unmodified
            Assert.Equal(DefaultReadme, File.ReadAllText(Path.Combine(tempFolderContext.Path, ProductFamilyReadmePath)));
            Assert.Equal(DefaultReadme, File.ReadAllText(Path.Combine(tempFolderContext.Path, RepoReadmePath)));
        }

        [Theory]
        [InlineData("IS_PRODUCT_FAMILY", "true", true)]
        [InlineData("IS_PRODUCT_FAMILY", "false")]
        [InlineData("FULL_REPO", "mcr.microsoft.com/dotnet/repo")]
        [InlineData("REPO", "dotnet/repo")]
        [InlineData("PARENT_REPO", "dotnet")]
        [InlineData("SHORT_REPO", "repo")]
        public void GenerateReadmesCommand_SupportedSymbols(string symbol, string expectedValue, bool isManifest = false)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateReadmesCommand command = InitializeCommand(tempFolderContext);

            IReadOnlyDictionary<Value, Value> symbols;
            if (isManifest)
            {
                symbols = command.GetSymbols(command.Manifest, ReadmeTemplatePath);
            }
            else
            {
                symbols = command.GetSymbols(command.Manifest.GetRepoByModelName("dotnet/repo"), ReadmeTemplatePath);
            }

            Value actualSymbolValue = symbols[symbol];
            Value expectedSymbolValue;
            if (actualSymbolValue.Type == ValueContent.Boolean)
            {
                expectedSymbolValue = Value.FromBoolean(bool.Parse(expectedValue));
            }
            else
            {
                expectedSymbolValue = Value.FromString(expectedValue);
            }

            Assert.Equal(expectedSymbolValue, actualSymbolValue);
        }

        private GenerateReadmesCommand InitializeCommand(
            TempFolderContext tempFolderContext,
            string readmeTemplate = ReadmeTemplate,
            string productFamilyReadme = DefaultReadme,
            string repoReadme = DefaultReadme,
            bool allowOptionalTemplates = true,
            bool validate = false)
        {
            DockerfileHelper.CreateFile(ProductFamilyReadmePath, tempFolderContext, productFamilyReadme);
            DockerfileHelper.CreateFile(RepoReadmePath, tempFolderContext, repoReadme);

            DockerfileHelper.CreateFile(AboutRepoTemplatePath, tempFolderContext, AboutRepoTemplate);

            string templatePath = null;
            if (readmeTemplate != null)
            {
                DockerfileHelper.CreateFile(ReadmeTemplatePath, tempFolderContext, readmeTemplate);
                templatePath = ReadmeTemplatePath;
            }

            Repo repo = CreateRepo("dotnet/repo");
            repo.Readme = RepoReadmePath;
            repo.ReadmeTemplate = templatePath;
            Manifest manifest = CreateManifest(repo);
            manifest.Registry = "mcr.microsoft.com";
            manifest.Readme = ProductFamilyReadmePath;
            manifest.ReadmeTemplate = templatePath;

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            Mock<IGitService> gitServiceMock = new Mock<IGitService>();
            _environmentServiceMock = new Mock<IEnvironmentService>();
            _environmentServiceMock
                .Setup(o => o.Exit(1))
                .Throws(_exitException);

            GenerateReadmesCommand command = new GenerateReadmesCommand(_environmentServiceMock.Object, gitServiceMock.Object);
            command.Options.Manifest = manifestPath;
            command.Options.AllowOptionalTemplates = allowOptionalTemplates;
            command.Options.Validate = validate;
            command.LoadManifest();

            return command;
        }
    }
}
