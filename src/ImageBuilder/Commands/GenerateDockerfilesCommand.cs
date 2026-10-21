#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GenerateDockerfilesCommand(IManifestJsonService manifestJsonService, IEnvironmentService environmentService, ILogger<GenerateDockerfilesCommand> logger) : GenerateArtifactsCommand<GenerateDockerfilesOptions>(manifestJsonService, environmentService, logger)
{
    private readonly ILogger _logger = logger;

    protected override string Description => "Generates the Dockerfiles from Cottle based templates (http://r3c.github.io/cottle/)";

    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GENERATING DOCKERFILES");

        await GenerateArtifactsAsync(
            Manifest.GetFilteredPlatforms(),
            (platform) => platform.DockerfileTemplate,
            (platform) => platform.DockerfilePath,
            (platform, templatePath, indent) => GetTemplateState(platform, templatePath, indent, cancellationToken),
            nameof(Platform.DockerfileTemplate),
            "Dockerfile",
            cancellationToken);

        ValidateArtifacts();
    }

    public (IReadOnlyDictionary<Value, Value> Symbols, string Indent) GetTemplateState(PlatformInfo platform, string templatePath, string indent, CancellationToken cancellationToken)
    {
        string versionedArch = platform.Model.Architecture.GetDisplayName(platform.Model.Variant);
        ImageInfo image = Manifest.GetImageByPlatform(platform);

        Dictionary<Value, Value> symbols = GetSymbols(
            templatePath,
            platform,
            (platform, templatePath, currentIndent) =>
                GetTemplateState(platform, templatePath, currentIndent + indent, cancellationToken),
            cancellationToken);
        symbols["ARCH_SHORT"] = platform.Model.Architecture.GetShortName();
        symbols["ARCH_NUPKG"] = platform.Model.Architecture.GetNupkgName();
        symbols["ARCH_VERSIONED"] = versionedArch;
        symbols["ARCH_TAG_SUFFIX"] = $"-{versionedArch}";
        symbols["PRODUCT_VERSION"] = image.ProductVersion;
        symbols["OS_VERSION"] = platform.Model.OsVersion;
        symbols["OS_VERSION_BASE"] = platform.BaseOsVersion;
        symbols["OS_VERSION_NUMBER"] = GetOsVersionNumber(platform);
        symbols["OS_ARCH_HYPHENATED"] = GetOsArchHyphenatedName(platform);

        return (symbols, indent);
    }

    private static string GetOsVersionNumber(PlatformInfo platform)
    {
        const string prefixGroup = "Prefix";
        const string versionGroup = "Version";
        const string ltscPrefix = "ltsc";
        Match match = Regex.Match(platform.Model.OsVersion, @$"(-(?<{prefixGroup}>[a-zA-Z_]*))?(?<{versionGroup}>\d+.\d+)");

        string versionNumber = string.Empty;
        if (match.Groups[prefixGroup].Success && match.Groups[prefixGroup].Value == ltscPrefix)
        {
            versionNumber = ltscPrefix;
        }

        versionNumber += match.Groups[versionGroup].Value;
        return versionNumber;
    }

    private static string GetOsArchHyphenatedName(PlatformInfo platform)
    {
        string osName;
        if (platform.BaseOsVersion.Contains("nanoserver"))
        {
            string version = platform.BaseOsVersion.Split('-')[1];
            osName = $"NanoServer-{version}";
        }
        else if (platform.BaseOsVersion.Contains("windowsservercore"))
        {
            string version = platform.BaseOsVersion.Split('-')[1];
            osName = $"WindowsServerCore-{version}";
        }
        else
        {
            osName = platform.GetOSDisplayName().Replace(' ', '-');
        }

        string archName = platform.Model.Architecture != Architecture.AMD64 ? $"-{platform.Model.Architecture.GetDisplayName()}" : string.Empty;

        return osName + archName;
    }
}
