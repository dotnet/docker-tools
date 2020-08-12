// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cottle;
using Cottle.Exceptions;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateDockerfilesCommand : ManifestCommand<GenerateDockerfilesOptions>
    {
        private readonly DocumentConfiguration _config = new DocumentConfiguration
        {
            BlockBegin = "{{",
            BlockContinue = "^",
            BlockEnd = "}}",
            Escape = '@',
            Trimmer = DocumentConfiguration.TrimNothing
        };

        private readonly IEnvironmentService _environmentService;

        [ImportingConstructor]
        public GenerateDockerfilesCommand(IEnvironmentService environmentService) : base()
        {
            _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        }

        public override async Task ExecuteAsync()
        {
            Logger.WriteHeading("GENERATING DOCKERFILES");

            List<string> outOfSyncDockerfiles = new List<string>();
            List<string> invalidTemplates = new List<string>();

            foreach (PlatformInfo platform in Manifest.GetFilteredPlatforms())
            {
                if (platform.DockerfileTemplate == null)
                {
                    if (Options.AllowOptionalTemplates)
                    {
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"The Dockerfile `{platform.DockerfilePath}` does not have a DockerfileTemplate specified.");
                }

                Logger.WriteSubheading($"Generating '{platform.DockerfilePath}' from '{platform.DockerfileTemplate}'");

                string template = await File.ReadAllTextAsync(platform.DockerfileTemplate);
                if (Options.IsVerbose)
                {
                    Logger.WriteMessage($"Template:{Environment.NewLine}{template}");
                }

                await GenerateDockerfileAsync(template, platform, outOfSyncDockerfiles, invalidTemplates);
            }

            if (outOfSyncDockerfiles.Any() || invalidTemplates.Any())
            {
                if (outOfSyncDockerfiles.Any())
                {
                    string dockerfileList = string.Join(Environment.NewLine, outOfSyncDockerfiles);
                    Logger.WriteError($"Dockerfiles out of sync with templates:{Environment.NewLine}{dockerfileList}");
                }

                if (invalidTemplates.Any())
                {
                    string templateList = string.Join(Environment.NewLine, invalidTemplates);
                    Logger.WriteError($"Invalid Templates:{Environment.NewLine}{templateList}");
                }

                _environmentService.Exit(1);
            }
        }

        private async Task GenerateDockerfileAsync(string template, PlatformInfo platform, List<string> outOfSyncDockerfiles, List<string> invalidTemplates)
        {
            try
            {
                IDocument document = Document.CreateDefault(template, _config).DocumentOrThrow;
                string generatedDockerfile = document.Render(Context.CreateBuiltin(GetSymbols(platform)));

                string currentDockerfile = File.Exists(platform.DockerfilePath) ?
                    await File.ReadAllTextAsync(platform.DockerfilePath) : string.Empty;
                if (currentDockerfile == generatedDockerfile)
                {
                    Logger.WriteMessage("Dockerfile in sync with template");
                }
                else if (Options.Validate)
                {
                    Logger.WriteError("Dockerfile out of sync with template");
                    outOfSyncDockerfiles.Add(platform.DockerfilePath);
                }
                else
                {
                    if (Options.IsVerbose)
                    {
                        Logger.WriteMessage($"Generated Dockerfile:{Environment.NewLine}{generatedDockerfile}");
                    }

                    if (!Options.IsDryRun)
                    {
                        await File.WriteAllTextAsync(platform.DockerfilePath, generatedDockerfile);
                        Logger.WriteMessage($"Updated '{platform.DockerfilePath}'");
                    }
                }
            }
            catch (ParseException e)
            {
                Logger.WriteError($"Error: {e}{Environment.NewLine}Invalid Syntax:{Environment.NewLine}{template.Substring(e.LocationStart)}");
                invalidTemplates.Add(platform.DockerfileTemplate);
            }
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

        public IReadOnlyDictionary<Value, Value> GetSymbols(PlatformInfo platform)
        {
            string versionedArch = platform.Model.Architecture.GetDisplayName(platform.Model.Variant);

            return new Dictionary<Value, Value>
            {
                ["ARCH_SHORT"] = platform.Model.Architecture.GetShortName(),
                ["ARCH_NUPKG"] = platform.Model.Architecture.GetNupkgName(),
                ["ARCH_VERSIONED"] = versionedArch,
                ["ARCH_TAG_SUFFIX"] = platform.Model.Architecture != Architecture.AMD64 ? $"-{versionedArch}" : string.Empty,
                ["OS_VERSION"] = platform.Model.OsVersion,
                ["OS_VERSION_BASE"] = platform.BaseOsVersion,
                ["OS_VERSION_NUMBER"] = Regex.Match(platform.Model.OsVersion, @"\d+.\d+").Value,
                ["OS_ARCH_HYPHENATED"] = GetOsArchHyphenatedName(platform),
                ["VARIABLES"] = Manifest.Model?.Variables?.ToDictionary(kvp => (Value)kvp.Key, kvp => (Value)kvp.Value)
            };
        }
    }
}
