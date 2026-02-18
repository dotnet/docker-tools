// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// ESRP signing service that invokes DDSignFiles.dll via the MicroBuild plugin.
/// </summary>
public class EsrpSigningService(
    IProcessService processService,
    ILoggerService logger,
    IEnvironmentService environmentService,
    IFileSystem fileSystem,
    IOptions<PublishConfiguration> publishConfigOptions) : IEsrpSigningService
{
    /// <summary>
    /// Environment variable set by MicroBuild plugin pointing to the signing tool location.
    /// </summary>
    private const string MBSignAppFolderEnv = "MBSIGN_APPFOLDER";

    /// <summary>
    /// Environment variable containing the base64-encoded SSL certificate for ESRP authentication.
    /// Required on Linux/macOS where there is no certificate store; set by the MicroBuild signing plugin.
    /// </summary>
    private const string VsEngEsrpSslEnv = "VSENGESRPSSL";

    /// <summary>
    /// The signing tool DLL provided by MicroBuild.
    /// </summary>
    private const string DDSignFilesDllName = "DDSignFiles.dll";

    private readonly IProcessService _processService = processService;
    private readonly ILoggerService _logger = logger;
    private readonly IEnvironmentService _environmentService = environmentService;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly SigningConfiguration? _signingConfig = publishConfigOptions.Value.Signing;

    /// <inheritdoc/>
    public async Task SignFilesAsync(
        IEnumerable<string> filePaths,
        int signingKeyCode,
        CancellationToken cancellationToken = default)
    {
        var files = filePaths.ToArray();
        if (files.Length == 0)
        {
            _logger.WriteMessage("No files to sign.");
            return;
        }

        var signType = _signingConfig?.SignType ?? "test";
        _logger.WriteMessage($"Signing {files.Length} files with certificate {signingKeyCode} (signType: {signType})");

        var mbsignAppFolder = _environmentService.GetEnvironmentVariable(MBSignAppFolderEnv)
            ?? throw new InvalidOperationException(
                $"{MBSignAppFolderEnv} environment variable is not set. Was the MicroBuild signing plugin installed?");

        // On non-Windows platforms, DDSignFiles.dll reads the SSL certificate from an environment variable
        // (there is no certificate store). Without it, DDSignFiles.dll retries auth endlessly until timeout.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && string.IsNullOrEmpty(_environmentService.GetEnvironmentVariable(VsEngEsrpSslEnv)))
        {
            throw new InvalidOperationException(
                $"{VsEngEsrpSslEnv} environment variable is not set. " +
                "On Linux, DDSignFiles.dll requires this for ESRP authentication. " +
                "Ensure the MicroBuild signing plugin environment variables are forwarded to the container.");
        }

        var signListTempPath = Path.Combine(Path.GetTempPath(), $"SignList_{Guid.NewGuid()}.json");
        try
        {
            var signListJson = GenerateSignListJson(files, signingKeyCode);
            await _fileSystem.WriteAllTextAsync(signListTempPath, signListJson, cancellationToken);

            var ddSignFilesPath = Path.Combine(mbsignAppFolder, DDSignFilesDllName);
            var args = $"--roll-forward major \"{ddSignFilesPath}\" -- /filelist:\"{signListTempPath}\" /signType:{signType}";

            // IProcessService.Execute is synchronous, so wrap in Task.Run.
            // The cancellation token prevents Task.Run from starting if already cancelled,
            // but IProcessService.Execute does not accept a token.
            await Task.Run(() =>
            {
                _processService.Execute(
                    "dotnet",
                    args,
                    isDryRun: false,
                    errorMessage: "ESRP signing failed");
            }, cancellationToken);

            _logger.WriteMessage("ESRP signing completed successfully.");
        }
        finally
        {
            if (_fileSystem.FileExists(signListTempPath))
            {
                _fileSystem.DeleteFile(signListTempPath);
            }
        }
    }

    /// <summary>
    /// Generates the sign list JSON required by DDSignFiles.dll.
    /// </summary>
    private static string GenerateSignListJson(string[] filePaths, int signingKeyCode)
    {
        var signFiles = new JsonArray();
        foreach (var filePath in filePaths)
        {
            signFiles.Add(new JsonObject
            {
                ["SrcPath"] = filePath,
                ["DstPath"] = filePath
            });
        }

        var root = new JsonObject
        {
            ["SignFileRecordList"] = new JsonArray
            {
                new JsonObject
                {
                    ["Certs"] = signingKeyCode.ToString(),
                    ["SignFileList"] = signFiles
                }
            }
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}
