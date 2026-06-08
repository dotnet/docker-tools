#nullable disable
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
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Shouldly;
using static Microsoft.DotNet.ImageBuilder.Tests.Helpers.ManifestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests
{
    [TestClass]
    public class GenerateDockerfilesCommandTests
    {
        private const string DockerfilePath = "1.0/sdk/os/Dockerfile";
        private const string DefaultDockerfile = "FROM Base";
        private const string DockerfileTemplatePath = "Dockerfile.Template";
        private const string DefaultDockerfileTemplate =
@"FROM Repo:2.1-{{OS_VERSION_BASE}}
ENV TEST1 {{if OS_VERSION = ""trixie-slim"":IfWorks}}
ENV TEST2 {{VARIABLES[""Variable1""]}}";
        private const string ExpectedDockerfile =
@"FROM Repo:2.1-trixie
ENV TEST1 IfWorks
ENV TEST2 Value1";

        private readonly Exception _exitException = new Exception();
        Mock<IEnvironmentService> _environmentServiceMock;

        public GenerateDockerfilesCommandTests()
        {
        }

        [TestMethod]
        public async Task GenerateDockerfilesCommand_Canonical()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext);

            await command.ExecuteAsync();

            string generatedDockerfile = File.ReadAllText(Path.Combine(tempFolderContext.Path, DockerfilePath));
            generatedDockerfile.ShouldBe(ExpectedDockerfile.NormalizeLineEndings(generatedDockerfile));
        }

        [TestMethod]
        public async Task GenerateDockerfilesCommand_InvalidTemplate()
        {
            string template = "FROM $REPO:2.1-{{if:}}";
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext, template);

            Exception actualException = await Should.ThrowAsync<Exception>(command.ExecuteAsync);

            actualException.ShouldBeSameAs(_exitException);
            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Once);
        }

        [TestMethod]
        public async Task GenerateDockerfilesCommand_MissingTemplate()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext, null, allowOptionalTemplates: false);

            await Should.ThrowAsync<InvalidOperationException>(command.ExecuteAsync);
        }

        /// <summary>
        /// Validates an exception is thrown if more than one unique template file is associated with a generated Dockerfile.
        /// </summary>
        [TestMethod]
        public async Task GenerateDockerfilesCommand_MismatchedTemplates()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            DockerfileHelper.CreateFile(DockerfilePath, tempFolderContext, DefaultDockerfile);

            string templatePath1 = "Dockerfile.Template1";
            DockerfileHelper.CreateFile(templatePath1, tempFolderContext, DefaultDockerfileTemplate);

            string templatePath2 = "Dockerfile.Template2";
            DockerfileHelper.CreateFile(templatePath2, tempFolderContext, DefaultDockerfileTemplate);

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag1" },
                                OS.Windows,
                                "nanoserver-1903",
                                dockerfileTemplatePath: templatePath1),
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag2" },
                                OS.Windows,
                                "windowsservercore-1903",
                                dockerfileTemplatePath: templatePath2)
                        },
                        productVersion: "1.2.3"
                    )
                )
            );

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            GenerateDockerfilesCommand command = new(TestHelper.CreateManifestJsonService(), Mock.Of<IEnvironmentService>(), Mock.Of<ILogger<GenerateDockerfilesCommand>>());
            command.Options.Manifest = manifestPath;
            command.LoadManifest();

            InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => command.ExecuteAsync());
            exception.Message.ShouldStartWith("Multiple unique template files are associated with the generated artifact path");
        }

        [TestMethod]
        public async Task GenerateDockerfilesCommand_Validate_UpToDate()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext, dockerfile: ExpectedDockerfile, validate: true);

            await command.ExecuteAsync();

            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task GenerateDockerfilesCommand_Validate_OutOfSync()
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext, validate: true);

            Exception actualException = await Should.ThrowAsync<Exception>(command.ExecuteAsync);

            actualException.ShouldBeSameAs(_exitException);
            _environmentServiceMock.Verify(o => o.Exit(It.IsAny<int>()), Times.Once);
            // Validate Dockerfile remains unmodified
            File.ReadAllText(Path.Combine(tempFolderContext.Path, DockerfilePath)).ShouldBe(DefaultDockerfile);
        }

        [TestMethod]
        [DataRow("repo1:tag1", "ARCH_SHORT", "arm")]
        [DataRow("repo1:tag1", "ARCH_NUPKG", "arm32")]
        [DataRow("repo1:tag1", "ARCH_VERSIONED", "arm32v7")]
        [DataRow("repo1:tag2", "ARCH_VERSIONED", "amd64")]
        [DataRow("repo1:tag3", "ARCH_VERSIONED", "amd64")]
        [DataRow("repo1:tag1", "ARCH_TAG_SUFFIX", "-arm32v7")]
        [DataRow("repo1:tag2", "ARCH_TAG_SUFFIX", "-amd64")]
        [DataRow("repo1:tag3", "ARCH_TAG_SUFFIX", "-amd64")]
        [DataRow("repo1:tag1", "PRODUCT_VERSION", "1.2.3")]
        [DataRow("repo1:tag1", "OS_VERSION", "trixie-slim")]
        [DataRow("repo1:tag2", "OS_VERSION", "nanoserver-1903")]
        [DataRow("repo1:tag3", "OS_VERSION", "windowsservercore-1903")]
        [DataRow("repo1:tag4", "OS_VERSION", "windowsservercore-ltsc2019")]
        [DataRow("repo1:tag1", "OS_VERSION_BASE", "trixie")]
        [DataRow("repo1:tag1", "OS_VERSION_NUMBER", "")]
        [DataRow("repo1:tag2", "OS_VERSION_NUMBER", "1903")]
        [DataRow("repo1:tag3", "OS_VERSION_NUMBER", "1903")]
        [DataRow("repo1:tag4", "OS_VERSION_NUMBER", "ltsc2019")]
        [DataRow("repo1:tag5", "OS_VERSION_NUMBER", "3.12")]
        [DataRow("repo1:tag6", "OS_VERSION_NUMBER", "1.0")]
        [DataRow("repo1:tag1", "OS_ARCH_HYPHENATED", "Debian-13-arm32")]
        [DataRow("repo1:tag2", "OS_ARCH_HYPHENATED", "NanoServer-1903")]
        [DataRow("repo1:tag3", "OS_ARCH_HYPHENATED", "WindowsServerCore-1903")]
        [DataRow("repo1:tag4", "OS_ARCH_HYPHENATED", "WindowsServerCore-ltsc2019")]
        [DataRow("repo1:tag5", "OS_ARCH_HYPHENATED", "Alpine-3.12")]
        [DataRow("repo1:tag6", "OS_ARCH_HYPHENATED", "CBL-Mariner-1.0")]
        [DataRow("repo1:tag1", "Variable1", "Value1", true)]
        public void GenerateDockerfilesCommand_SupportedSymbols(string tag, string symbol, string expectedValue, bool isVariable = false)
        {
            using TempFolderContext tempFolderContext = TestHelper.UseTempFolder();
            GenerateDockerfilesCommand command = InitializeCommand(tempFolderContext);

            (IReadOnlyDictionary<Value, Value> symbols, string indent) = command.GetTemplateState(
                command.Manifest.GetPlatformByTag(tag), DockerfileTemplatePath, string.Empty);

            Value variableValue;
            if (isVariable)
            {
                variableValue = symbols["VARIABLES"].Fields[symbol];
            }
            else
            {
                variableValue = symbols[symbol];
            }

            variableValue.ShouldBe(expectedValue);
        }

        private GenerateDockerfilesCommand InitializeCommand(
            TempFolderContext tempFolderContext,
            string dockerfileTemplate = DefaultDockerfileTemplate,
            string dockerfile = DefaultDockerfile,
            bool allowOptionalTemplates = true,
            bool validate = false)
        {
            DockerfileHelper.CreateFile(DockerfilePath, tempFolderContext, dockerfile);

            string templatePath = null;
            if (dockerfileTemplate != null)
            {
                DockerfileHelper.CreateFile(DockerfileTemplatePath, tempFolderContext, dockerfileTemplate);
                templatePath = DockerfileTemplatePath;
            }

            Manifest manifest = CreateManifest(
                CreateRepo("repo1",
                    CreateImage(
                        new Platform[]
                        {
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag1" },
                                OS.Linux,
                                "trixie-slim",
                                Architecture.ARM,
                                "v7",
                                dockerfileTemplatePath: templatePath),
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag2" },
                                OS.Windows,
                                "nanoserver-1903"),
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag3" },
                                OS.Windows,
                                "windowsservercore-1903"),
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag4" },
                                OS.Windows,
                                "windowsservercore-ltsc2019"),
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag5" },
                                OS.Linux,
                                "alpine3.12"),
                            CreatePlatform(
                                DockerfilePath,
                                new string[] { "tag6" },
                                OS.Linux,
                                "cbl-mariner1.0")
                        },
                        productVersion: "1.2.3"
                    )
                )
            );
            AddVariable(manifest, "Variable1", "Value1");

            string manifestPath = Path.Combine(tempFolderContext.Path, "manifest.json");
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest));

            _environmentServiceMock = new Mock<IEnvironmentService>();
            _environmentServiceMock
                .Setup(o => o.Exit(1))
                .Throws(_exitException);

            GenerateDockerfilesCommand command = new GenerateDockerfilesCommand(TestHelper.CreateManifestJsonService(), _environmentServiceMock.Object, Mock.Of<ILogger<GenerateDockerfilesCommand>>());
            command.Options.Manifest = manifestPath;
            command.Options.AllowOptionalTemplates = allowOptionalTemplates;
            command.Options.Validate = validate;
            command.LoadManifest();

            return command;
        }
    }
}
