// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cottle;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateDockerfilesCommand : GenerateArtifactsCommand<GenerateDockerfilesOptions>
    {

        [ImportingConstructor]
        public GenerateDockerfilesCommand(IEnvironmentService environmentService) : base(environmentService)
        {
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING DOCKERFILES");

            await GenerateArtifactsAsync(
                Manifest.GetFilteredPlatforms(),
                (platform) => platform.DockerfileTemplate,
                (platform) => platform.DockerfilePath,
                (platform) => GetSymbols(platform),
                nameof(Models.Manifest.Platform.DockerfileTemplate),
                "Dockerfile");

            ValidateArtifacts();
        }

        public Dictionary<Value, Value> GetSymbols(PlatformInfo platform)
        {
            string versionedArch = platform.Model.Architecture.GetDisplayName(platform.Model.Variant);

            Dictionary<Value, Value> symbols = GetSymbols();
            symbols["ARCH_SHORT"] = platform.Model.Architecture.GetShortName();
            symbols["ARCH_NUPKG"] = platform.Model.Architecture.GetNupkgName();
            symbols["ARCH_VERSIONED"] = versionedArch;
            symbols["ARCH_TAG_SUFFIX"] = platform.Model.Architecture != Architecture.AMD64 ? $"-{versionedArch}" : string.Empty;
            symbols["OS_VERSION"] = platform.Model.OsVersion;
            symbols["OS_VERSION_BASE"] = platform.BaseOsVersion;
            symbols["OS_VERSION_NUMBER"] = Regex.Match(platform.Model.OsVersion, @"\d+.\d+").Value;
            symbols["OS_ARCH_HYPHENATED"] = GetOsArchHyphenatedName(platform);

            return symbols;
        }

        private static string GetOsArchHyphenatedName(PlatformInfo platform)
        {
            string osName;
            if (platform.BaseOsVersion.Contains("nanoserver"))
            {
                string version = platform.BaseOsVersion.Split('-')[1];
                osName = $"NanoServer-{version}";
            }
            else
            {
                osName = platform.GetOSDisplayName().Replace(' ', '-');
            }

            string archName = platform.Model.Architecture != Architecture.AMD64 ? $"-{platform.Model.Architecture.GetDisplayName()}" : string.Empty;

            return osName + archName;
        }
    }
}
