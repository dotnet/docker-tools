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
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests.Signing;

public class EsrpSigningServiceTests
{
    private const string MBSignAppFolderEnv = "MBSIGN_APPFOLDER";
    private const string VsEngEsrpSslEnv = "VSENGESRPSSL";

    [Fact]
    public async Task SignFilesAsync_EmptyFileList_ReturnsWithoutSigning()
    {
        var mockProcess = new Mock<IProcessService>();
        var service = CreateService(mockProcess: mockProcess);

        await service.SignFilesAsync([], signingKeyCode: 100);

        mockProcess.Verify(
            p => p.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SignFilesAsync_MissingMbSignAppFolder_ThrowsInvalidOperation()
    {
        var mockEnv = new Mock<IEnvironmentService>();
        mockEnv.Setup(e => e.GetEnvironmentVariable(MBSignAppFolderEnv)).Returns((string?)null);

        var service = CreateService(mockEnv: mockEnv);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100));

        ex.Message.ShouldContain(MBSignAppFolderEnv);
    }

    [Fact]
    public async Task SignFilesAsync_InvokesProcessWithCorrectArgs()
    {
        var mockProcess = new Mock<IProcessService>();
        var mockEnv = CreateEnvironmentWithRequiredVars();

        var service = CreateService(mockProcess: mockProcess, mockEnv: mockEnv);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 42);

        mockProcess.Verify(
            p => p.Execute(
                "dotnet",
                It.Is<string>(args =>
                    args.Contains("DDSignFiles.dll") &&
                    args.Contains("/signType:test") &&
                    args.Contains("--roll-forward major")),
                false,
                "ESRP signing failed",
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task SignFilesAsync_CleansUpTempFileAfterSigning()
    {
        var mockEnv = CreateEnvironmentWithRequiredVars();
        var fileSystem = new InMemoryFileSystem();

        var service = CreateService(mockEnv: mockEnv, fileSystem: fileSystem);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100);

        // The sign list temp file should be written then deleted
        fileSystem.FilesWritten.Count.ShouldBe(1);
        fileSystem.FilesDeleted.Count.ShouldBe(1);
        fileSystem.FilesDeleted.First().ShouldBe(fileSystem.FilesWritten.First());
    }

    [Fact]
    public async Task SignFilesAsync_CleansUpTempFileOnFailure()
    {
        var mockEnv = CreateEnvironmentWithRequiredVars();
        var mockProcess = new Mock<IProcessService>();
        mockProcess
            .Setup(p => p.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("signing failed"));

        var fileSystem = new InMemoryFileSystem();

        var service = CreateService(mockProcess: mockProcess, mockEnv: mockEnv, fileSystem: fileSystem);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100));

        fileSystem.FilesDeleted.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SignFilesAsync_WritesSignListJson()
    {
        var mockEnv = CreateEnvironmentWithRequiredVars();
        var fileSystem = new InMemoryFileSystem();

        var service = CreateService(mockEnv: mockEnv, fileSystem: fileSystem);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100);

        fileSystem.FilesWritten.Count.ShouldBe(1);
        var signListPath = fileSystem.FilesWritten.First();
        signListPath.ShouldContain("SignList_");
        signListPath.ShouldEndWith(".json");

        // File is deleted after signing, but we can check the DeletedFiles list confirms cleanup
        fileSystem.FilesDeleted.ShouldContain(signListPath);
    }

    private static Mock<IEnvironmentService> CreateEnvironmentWithRequiredVars()
    {
        var mockEnv = new Mock<IEnvironmentService>();
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
        var publishConfig = new PublishConfiguration { Signing = signingConfig };

        mockEnv ??= CreateEnvironmentWithRequiredVars();

        return new EsrpSigningService(
            (mockProcess ?? new Mock<IProcessService>()).Object,
            Mock.Of<ILogger<EsrpSigningService>>(),
            mockEnv.Object,
            fileSystem ?? new InMemoryFileSystem(),
            Options.Create(publishConfig));
    }
}
