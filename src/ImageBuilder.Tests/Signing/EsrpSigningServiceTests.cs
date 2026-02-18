// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.DotNet.ImageBuilder.Signing;
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
        var mockFileSystem = new Mock<IFileSystem>();

        var service = CreateService(mockProcess: mockProcess, mockEnv: mockEnv, mockFileSystem: mockFileSystem);

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
        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);

        var service = CreateService(mockEnv: mockEnv, mockFileSystem: mockFileSystem);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100);

        mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SignFilesAsync_CleansUpTempFileOnFailure()
    {
        var mockEnv = CreateEnvironmentWithRequiredVars();
        var mockProcess = new Mock<IProcessService>();
        mockProcess
            .Setup(p => p.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("signing failed"));

        var mockFileSystem = new Mock<IFileSystem>();
        mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);

        var service = CreateService(mockProcess: mockProcess, mockEnv: mockEnv, mockFileSystem: mockFileSystem);

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100));

        mockFileSystem.Verify(fs => fs.DeleteFile(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SignFilesAsync_WritesSignListJson()
    {
        var mockEnv = CreateEnvironmentWithRequiredVars();
        var mockFileSystem = new Mock<IFileSystem>();

        var service = CreateService(mockEnv: mockEnv, mockFileSystem: mockFileSystem);

        await service.SignFilesAsync(["/tmp/file.payload"], signingKeyCode: 100);

        mockFileSystem.Verify(
            fs => fs.WriteAllTextAsync(
                It.Is<string>(path => path.Contains("SignList_") && path.EndsWith(".json")),
                It.Is<string>(json => json.Contains("SignFileRecordList") && json.Contains("/tmp/file.payload")),
                It.IsAny<CancellationToken>()),
            Times.Once);
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
        Mock<IFileSystem>? mockFileSystem = null,
        SigningConfiguration? signingConfig = null)
    {
        signingConfig ??= new SigningConfiguration { Enabled = true, SignType = "test" };
        var publishConfig = new PublishConfiguration { Signing = signingConfig };

        mockEnv ??= CreateEnvironmentWithRequiredVars();

        return new EsrpSigningService(
            (mockProcess ?? new Mock<IProcessService>()).Object,
            Mock.Of<ILogger<EsrpSigningService>>(),
            mockEnv.Object,
            (mockFileSystem ?? new Mock<IFileSystem>()).Object,
            Options.Create(publishConfig));
    }
}
