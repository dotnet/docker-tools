// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// ESRP signing service that invokes DDSignFiles.dll via the MicroBuild plugin.
/// </summary>
public class EsrpSigningService(
    IProcessService processService,
    ILogger<EsrpSigningService> logger,
    IEnvironmentService environmentService,
    IFileSystem fileSystem,
    IOptions<PublishConfiguration> publishConfigOptions) : IEsrpSigningService
{
    /// <summary>
    /// Environment variable set by MicroBuild plugin pointing to the signing tool location.
    /// </summary>
    private const string MBSignAppFolderEnv = "MBSIGN_APPFOLDER";

    /// <summary>
    /// The signing tool DLL provided by MicroBuild.
    /// </summary>
    private const string DDSignFilesDllName = "DDSignFiles.dll";

    private readonly IProcessService _processService = processService;
    private readonly ILogger<EsrpSigningService> _logger = logger;
    private readonly IEnvironmentService _environmentService = environmentService;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly SigningConfiguration? _signingConfig = publishConfigOptions.Value.Signing;

    /// <inheritdoc/>
    public async Task SignFilesAsync(
        IEnumerable<string> filePaths,
        int signingKeyCode,
        CancellationToken cancellationToken = default)
    {
        string[] filesToSign = filePaths.ToArray();
        if (filesToSign.Length == 0)
        {
            _logger.LogInformation("No files to sign.");
            return;
        }

        string signType = _signingConfig?.SignType ?? "test";
        _logger.LogInformation(
            "Signing {Count} files with certificate {KeyCode} (signType: {SignType})",
            filesToSign.Length, signingKeyCode, signType);

        string microBuildSigningAppDir = _environmentService.GetEnvironmentVariable(MBSignAppFolderEnv)
            ?? throw new InvalidOperationException(
                $"{MBSignAppFolderEnv} environment variable is not set. Was the MicroBuild signing plugin installed?");
        string ddSignFilesDllPath = Path.Combine(microBuildSigningAppDir, DDSignFilesDllName);

        string signListFileName = $"SignList_{Guid.NewGuid()}.json";
        string signListFilePath = Path.Combine(Path.GetTempPath(), signListFileName);

        string signListJsonContent = GenerateSignListJson(filesToSign, signingKeyCode);
        await _fileSystem.WriteAllTextAsync(signListFilePath, signListJsonContent, cancellationToken);

        string[] args =
        [
            "--roll-forward",
            "major",
            $"\"{ddSignFilesDllPath}\"",
            "--",
            $"/filelist:\"{signListFilePath}\"",
            $"/signType:{signType}"
        ];

        try
        {
            _processService.Execute(
                fileName: "dotnet",
                args: string.Join(' ', args),
                isDryRun: false,
                errorMessage: "ESRP signing failed");

            _logger.LogInformation("ESRP signing completed.");
        }
        finally
        {
            _fileSystem.DeleteFile(signListFilePath);
        }
    }

    /// <summary>
    /// Generates the signing list JSON required by DDSignFiles.dll.
    /// </summary>
    private static string GenerateSignListJson(IEnumerable<string> payloadFilePaths, int signingKeyCode)
    {
        IEnumerable<SignFileEntry> signFiles =
            payloadFilePaths.Select(path => new SignFileEntry(SrcPath: path, DstPath: path));

        string keycodeString = signingKeyCode.ToString();
        var record = new SignFileRecord(Certs: keycodeString, SignFileList: signFiles);
        var signList = new SignList(SignFileRecordList: [record]);

        return JsonSerializer.Serialize(signList, s_esrpJsonOptions);
    }

    // Models for ESRP/MicroBuild signing service
    private static readonly JsonSerializerOptions s_esrpJsonOptions = new() { WriteIndented = true };
    private record SignList(IEnumerable<SignFileRecord> SignFileRecordList);
    private record SignFileRecord(string Certs, IEnumerable<SignFileEntry> SignFileList);
    private record SignFileEntry(string SrcPath, string DstPath);
}
