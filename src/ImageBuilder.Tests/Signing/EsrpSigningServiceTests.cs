// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Signing;
using Microsoft.DotNet.ImageBuilder.Tests.Helpers;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

[TestClass]
public class EsrpSigningServiceTests
{
    public TestContext? TestContext { get; set; }

    private const string MBSignAppFolderEnv = "MBSIGN_APPFOLDER";
    private const string VsEngEsrpSslEnv = "VSENGESRPSSL";

    [TestMethod]
    public async Task SignFilesAsync_EmptyFileList_ReturnsWithoutSigning()
    {
        Mock<IProcessService> mockProcess = new Mock<IProcessService>();
        EsrpSigningService service = CreateService(mockProcess: mockProcess);

        await service.SignFilesAsync([], signingKeyCode: 100, TestContext?.CancellationToken ?? default);

        mockProcess.Verify(
            p => p.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task SignFilesAsync_MissingMbSignAppFolder_ThrowsInvalidOperation()
    {
        Mock<IEnvironmentService> mockEnv = new Mock<IEnvironmentService>();
        mockEnv.Setup(e => e.GetEnvironmentVariable(MBSignAppFolderEnv)).Returns((string?)null);

        EsrpSigningService service = CreateService(mockEnv: mockEnv);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100, TestContext?.CancellationToken ?? default));

        ex.Message.ShouldContain(MBSignAppFolderEnv);
    }

    [TestMethod]
    public async Task SignFilesAsync_InvokesProcessWithCorrectArgs()
    {
        Mock<IProcessService> mockProcess = new Mock<IProcessService>();
        Mock<IEnvironmentService> mockEnv = CreateEnvironmentWithRequiredVars();

        EsrpSigningService service = CreateService(mockProcess: mockProcess, mockEnv: mockEnv);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 42, TestContext?.CancellationToken ?? default);

        mockProcess.Verify(
            p => p.Execute(
                "dotnet",
                It.Is<string>(args =>
                    args.Contains("DDSignFiles.dll") &&
                    args.Contains("/signType:test") &&
                    args.Contains("--roll-forward major")),
                false,
                It.IsAny<CancellationToken>(),
                "ESRP signing failed",
                It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SignFilesAsync_CleansUpTempFileAfterSigning()
    {
        Mock<IEnvironmentService> mockEnv = CreateEnvironmentWithRequiredVars();
        InMemoryFileSystem fileSystem = new InMemoryFileSystem();

        EsrpSigningService service = CreateService(mockEnv: mockEnv, fileSystem: fileSystem);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100, TestContext?.CancellationToken ?? default);

        // The sign list temp file should be written then deleted
        fileSystem.FilesWritten.Count.ShouldBe(1);
        fileSystem.FilesDeleted.Count.ShouldBe(1);
        fileSystem.FilesDeleted.First().ShouldBe(fileSystem.FilesWritten.First());
    }

    [TestMethod]
    public async Task SignFilesAsync_CleansUpTempFileOnFailure()
    {
        Mock<IEnvironmentService> mockEnv = CreateEnvironmentWithRequiredVars();
        Mock<IProcessService> mockProcess = new Mock<IProcessService>();
        mockProcess
            .Setup(p => p.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("signing failed"));

        InMemoryFileSystem fileSystem = new InMemoryFileSystem();

        EsrpSigningService service = CreateService(mockProcess: mockProcess, mockEnv: mockEnv, fileSystem: fileSystem);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100, TestContext?.CancellationToken ?? default));

        fileSystem.FilesDeleted.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task SignFilesAsync_WritesSignListJson()
    {
        Mock<IEnvironmentService> mockEnv = CreateEnvironmentWithRequiredVars();
        InMemoryFileSystem fileSystem = new InMemoryFileSystem();

        EsrpSigningService service = CreateService(mockEnv: mockEnv, fileSystem: fileSystem);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100, TestContext?.CancellationToken ?? default);

        fileSystem.FilesWritten.Count.ShouldBe(1);
        string signListPath = fileSystem.FilesWritten.First();
        signListPath.ShouldContain("SignList_");
        signListPath.ShouldEndWith(".json");

        // File is deleted after signing, but we can check the DeletedFiles list confirms cleanup
        fileSystem.FilesDeleted.ShouldContain(signListPath);
    }

    private static Mock<IEnvironmentService> CreateEnvironmentWithRequiredVars()
    {
        Mock<IEnvironmentService> mockEnv = new Mock<IEnvironmentService>();
        mockEnv.Setup(e => e.GetEnvironmentVariable(MBSignAppFolderEnv)).Returns("/opt/mbsign");
        mockEnv.Setup(e => e.GetEnvironmentVariable(VsEngEsrpSslEnv)).Returns("base64cert");
        return mockEnv;
    }

    private static EsrpSigningService CreateService(
        Mock<IProcessService>? mockProcess = null,
        Mock<IEnvironmentService>? mockEnv = null,
        InMemoryFileSystem? fileSystem = null,
        SigningConfiguration? signingConfig = null)
    {
        signingConfig ??= new SigningConfiguration { Enabled = true, SignType = "test" };
        PublishConfiguration publishConfig = new PublishConfiguration { Signing = signingConfig };

        mockEnv ??= CreateEnvironmentWithRequiredVars();

        return new EsrpSigningService(
            (mockProcess ?? new Mock<IProcessService>()).Object,
            Mock.Of<ILogger<EsrpSigningService>>(),
            mockEnv.Object,
            fileSystem ?? new InMemoryFileSystem(),
            Options.Create(publishConfig));
    }
}
