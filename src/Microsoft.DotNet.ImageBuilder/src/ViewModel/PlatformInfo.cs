// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class PlatformInfo
    {
        private const string ArgGroupName = "arg";
        private const string FromImageMatchName = "fromImage";
        private const string StageIdMatchName = "stageId";
        private static Regex FromRegex { get; } = new Regex($@"FROM\s+(?<{FromImageMatchName}>\S+)(\s+AS\s+(?<{StageIdMatchName}>\S+))?");

        private static readonly string s_argPattern = $"\\$(?<{ArgGroupName}>[\\w\\d-_]+)";

        private List<string> _overriddenFromImages = new();
        private IEnumerable<string> _internalRepos = Enumerable.Empty<string>();

        public string BaseOsVersion { get; private set; }
        public IDictionary<string, string?> BuildArgs { get; private set; } = ImmutableDictionary<string, string?>.Empty;
        public string BuildContextPath { get; private set; }
        public string DockerfilePath { get; private set; }
        public string DockerfilePathRelativeToManifest { get; private set; }
        public string? DockerfileTemplate { get; private set; }
        public string FinalStageFromImage { get; private set; } = string.Empty;
        public IEnumerable<string> ExternalFromImages { get; private set; } = Enumerable.Empty<string>();
        public IEnumerable<string> InternalFromImages { get; private set; } = Enumerable.Empty<string>();
        public Platform Model { get; private set; }
        public IEnumerable<string> OverriddenFromImages { get => _overriddenFromImages; }
        public string FullRepoModelName { get; set; }
        private string RepoName { get; set; }
        public IEnumerable<TagInfo> Tags { get; private set; }
        public IDictionary<string, CustomBuildLegGroup> CustomLegGroups { get; private set; } =
            ImmutableDictionary<string, CustomBuildLegGroup>.Empty;
        private VariableHelper VariableHelper { get; set; }

        private PlatformInfo(Platform model, string baseOsVersion, string fullRepoModelName, string repoName, VariableHelper variableHelper,
            string baseDirectory)
        {
            Model = model;
            BaseOsVersion = baseOsVersion;
            FullRepoModelName = fullRepoModelName;
            RepoName = repoName;
            VariableHelper = variableHelper;

            string dockerfileWithBaseDir = Path.Combine(baseDirectory, model.ResolveDockerfilePath(baseDirectory));
            DockerfilePath = PathHelper.NormalizePath(dockerfileWithBaseDir);
            BuildContextPath = PathHelper.NormalizePath(Path.GetDirectoryName(dockerfileWithBaseDir));
            DockerfilePathRelativeToManifest = PathHelper.TrimPath(baseDirectory, DockerfilePath);

            if (model.DockerfileTemplate != null)
            {
                DockerfileTemplate = Path.Combine(baseDirectory, model.DockerfileTemplate);
            }

            Tags = model.Tags
                .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper, BuildContextPath))
                .ToArray();
        }

        public static PlatformInfo Create(Platform model, string fullRepoModelName, string repoName, VariableHelper variableHelper, string baseDirectory) =>
            new(
                model,
                model.OsVersion.TrimEnd("-slim"),
                fullRepoModelName,
                repoName,
                variableHelper,
                baseDirectory);

        public void Initialize(IEnumerable<string> internalRepos, string registry)
        {
            _internalRepos = internalRepos;
            InitializeBuildArgs();
            InitializeFromImages();

            CustomLegGroups = Model.CustomBuildLegGroups
                .Select(group =>
                    new CustomBuildLegGroup
                    {
                        Name = group.Name,
                        Type = group.Type,
                        Dependencies = group.Dependencies
                            .Select(dependency => VariableHelper.SubstituteValues(dependency))
                            .ToArray()
                    })
                .ToDictionary(info => info.Name)
            ;
        }

        private void InitializeBuildArgs()
        {
            if (Model.BuildArgs != null)
            {
                BuildArgs = Model.BuildArgs.ToDictionary(kvp => kvp.Key, kvp => VariableHelper.SubstituteValues(kvp.Value));
            }
        }

        private void InitializeFromImages()
        {
            string dockerfile = File.ReadAllText(DockerfilePath);
            IList<Match> fromMatches = FromRegex.Matches(dockerfile);

            if (!fromMatches.Any())
            {
                throw new InvalidOperationException($"Unable to find a FROM image in {DockerfilePath}.");
            }

            IEnumerable<string> fromImages = fromMatches
                .Select(match => match.Groups[FromImageMatchName].Value)
                .Select(from => SubstituteOverriddenRepo(from))
                .Select(from => SubstituteBuildArgs(from))
                .Where(from => !IsStageReference(from, fromMatches))
                .ToArray();

            FinalStageFromImage = fromImages.Last();

            InternalFromImages = fromImages
                .Where(from => IsInternalFromImage(from))
                .ToArray();
            ExternalFromImages = fromImages
                .Except(InternalFromImages)
                .ToArray();
        }

        public bool IsInternalFromImage(string fromImage)
        {
            return _internalRepos.Any(repo => fromImage.StartsWith($"{repo}:"));
        }

        public string GetOSDisplayName()
        {
            string displayName;
            string os = BaseOsVersion;

            if (Model.OS == OS.Windows)
            {
                string version = os.Split('-')[1];
                if (os.StartsWith("nanoserver"))
                {
                    displayName = GetWindowsVersionDisplayName("Nano Server", version);
                }
                else if (os.StartsWith("windowsservercore"))
                {
                    displayName = GetWindowsVersionDisplayName("Windows Server Core", version);
                }
                else
                {
                    throw new NotSupportedException($"The OS version '{os}' is not supported.");
                }
            }
            else
            {
                if (os.Contains("debian"))
                {
                    displayName = "Debian";
                }
                else if (os.Contains("jessie"))
                {
                    displayName = "Debian 8";
                }
                else if (os.Contains("stretch"))
                {
                    displayName = "Debian 9";
                }
                else if (os.Contains("buster"))
                {
                    displayName = "Debian 10";
                }
                else if (os.Contains("bullseye"))
                {
                    displayName = "Debian 11";
                }
                else if (os.Contains("xenial"))
                {
                    displayName = "Ubuntu 16.04";
                }
                else if (os.Contains("bionic"))
                {
                    displayName = "Ubuntu 18.04";
                }
                else if (os.Contains("disco"))
                {
                    displayName = "Ubuntu 19.04";
                }
                else if (os.Contains("focal"))
                {
                    displayName = "Ubuntu 20.04";
                }
                else if (os.Contains("hirsute"))
                {
                    displayName = "Ubuntu 21.04";
                }
                else if (os.Contains("alpine") || os.Contains("centos") || os.Contains("fedora"))
                {
                    displayName = FormatVersionableOsName(os, name => name.FirstCharToUpper());
                }
                else if (os.Contains("cbl-mariner"))
                {
                    displayName = FormatVersionableOsName(os, name => "CBL-Mariner");
                }
                else if (os.Contains("leap"))
                {
                    displayName = FormatVersionableOsName(os, name => "openSUSE Leap");
                }
                else
                {
                    throw new NotSupportedException($"The OS version '{os}' is not supported.");
                }
            }

            return displayName;
        }

        private static string GetWindowsVersionDisplayName(string windowsName, string version)
        {
            if (version.StartsWith("ltsc"))
            {
                return $"{windowsName} {version.TrimStart("ltsc")}";
            }
            else
            {
                return $"{windowsName}, version {version}";
            }
        }

        public static bool AreMatchingPlatforms(ImageInfo image1, PlatformInfo platform1, ImageInfo image2, PlatformInfo platform2) =>
            platform1.GetUniqueKey(image1) == platform2.GetUniqueKey(image2);

        public string GetUniqueKey(ImageInfo parentImageInfo) =>
            $"{DockerfilePathRelativeToManifest}-{Model.OS}-{Model.OsVersion}-{Model.Architecture}-{parentImageInfo.ProductVersion}";

        private static string FormatVersionableOsName(string os, Func<string, string> formatName)
        {
            (string osName, string osVersion) = GetOsVersionInfo(os);
            if (string.IsNullOrEmpty(osVersion))
            {
                return formatName(osName);
            }
            else
            {
                return $"{formatName(osName)} {osVersion}";
            }
        }

        private static (string Name, string Version) GetOsVersionInfo(string osVersion)
        {
            int versionIndex = osVersion.IndexOfAny(new char[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' });
            if (versionIndex != -1)
            {
                return (osVersion.Substring(0, versionIndex), osVersion.Substring(versionIndex));
            }

            return (osVersion, string.Empty);
        }
            

        private static bool IsStageReference(string fromImage, IList<Match> fromMatches)
        {
            bool isStageReference = false;

            foreach (Match fromMatch in fromMatches)
            {
                if (string.Equals(fromImage, fromMatch.Groups[FromImageMatchName].Value, StringComparison.Ordinal))
                {
                    // Stage references can only be to previous stages so once the fromImage is reached, stop searching.
                    break;
                }

                Group stageIdGroup = fromMatch.Groups[StageIdMatchName];
                if (stageIdGroup.Success && string.Equals(fromImage, stageIdGroup.Value, StringComparison.Ordinal))
                {
                    isStageReference = true;
                    break;
                }
            }

            return isStageReference;
        }

        private string SubstituteBuildArgs(string instruction)
        {
            foreach (Match match in Regex.Matches(instruction, s_argPattern))
            {
                if (!BuildArgs.TryGetValue(match.Groups[ArgGroupName].Value, out string? argValue))
                {
                    throw new InvalidOperationException(
                        $"A value was not found for the ARG '{match.Value}' in `{DockerfilePath}`");
                }

                instruction = instruction.Replace(match.Value, argValue);
            }

            return instruction;
        }

        private string SubstituteOverriddenRepo(string from)
        {
            if (RepoName != FullRepoModelName && from.StartsWith($"{FullRepoModelName}:"))
            {
                _overriddenFromImages.Add(from);
                from = DockerHelper.ReplaceRepo(from, RepoName);
            }

            return from;
        }
    }
}
#nullable disable
