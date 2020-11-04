// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Valleysoft.DockerfileModel;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class PlatformInfo
    {
        private const string ArgGroupName = "arg";
        private const string FromImageMatchName = "fromImage";
        private const string StageIdMatchName = "stageId";

        private static readonly string s_argPattern = $"\\$(?<{ArgGroupName}>[\\w\\d-_]+)";

        private List<string> _overriddenFromImages;
        private IEnumerable<string> _internalRepos;

        public string BaseOsVersion { get; private set; }
        public IDictionary<string, string> BuildArgs { get; private set; }
        public string BuildContextPath { get; private set; }
        public string DockerfilePath { get; private set; }
        public string DockerfilePathRelativeToManifest { get; private set; }
        public string DockerfileTemplate { get; private set; }
        public string FinalStageFromImage { get; private set; }
        public IEnumerable<string> ExternalFromImages { get; private set; }
        public IEnumerable<string> InternalFromImages { get; private set; }
        public Platform Model { get; private set; }
        public IEnumerable<string> OverriddenFromImages { get => _overriddenFromImages; }
        public string FullRepoModelName { get; set; }
        private string RepoName { get; set; }
        public IEnumerable<TagInfo> Tags { get; private set; }
        public IDictionary<string, CustomBuildLegGroup> CustomLegGroups { get; private set; }
        private VariableHelper VariableHelper { get; set; }

        public static PlatformInfo Create(Platform model, string fullRepoModelName, string repoName, VariableHelper variableHelper, string baseDirectory)
        {
            PlatformInfo platformInfo = new PlatformInfo
            {
                BaseOsVersion = model.OsVersion.TrimEnd("-slim"),
                FullRepoModelName = fullRepoModelName,
                Model = model,
                RepoName = repoName,
                VariableHelper = variableHelper
            };

            string dockerfileWithBaseDir = Path.Combine(baseDirectory, model.ResolveDockerfilePath(baseDirectory));

            platformInfo.DockerfilePath = PathHelper.NormalizePath(dockerfileWithBaseDir);
            platformInfo.BuildContextPath = PathHelper.NormalizePath(Path.GetDirectoryName(dockerfileWithBaseDir));
            platformInfo.DockerfilePathRelativeToManifest = PathHelper.TrimPath(baseDirectory, platformInfo.DockerfilePath);

            if (model.DockerfileTemplate != null)
            {
                platformInfo.DockerfileTemplate = Path.Combine(baseDirectory, model.DockerfileTemplate);
            }

            platformInfo.Tags = model.Tags
                .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper, platformInfo.BuildContextPath))
                .ToArray();

            return platformInfo;
        }

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
            if (Model.BuildArgs == null)
            {
                BuildArgs = ImmutableDictionary<string, string>.Empty;
            }
            else
            {
                BuildArgs = Model.BuildArgs.ToDictionary(kvp => kvp.Key, kvp => VariableHelper.SubstituteValues(kvp.Value));
            }
        }

        private void InitializeFromImages()
        {
            _overriddenFromImages = new List<string>();

            Dockerfile dockerfile = Dockerfile.Parse(File.ReadAllText(DockerfilePath));

            IEnumerable<FromInstruction> fromInstructions = dockerfile.Items
                .OfType<FromInstruction>()
                .ToArray();

            if (!fromInstructions.Any())
            {
                throw new InvalidOperationException($"Unable to find a FROM image in {DockerfilePath}.");
            }

            foreach (FromInstruction fromInstruction in fromInstructions)
            {
                fromInstruction.ImageName = SubstituteOverriddenRepo(fromInstruction.ImageName).ToString();
                fromInstruction.ResolveVariables(dockerfile.EscapeChar, BuildArgs, new ResolutionOptions { UpdateInline = true });
            }

            IEnumerable<string> stageNames = new StagesView(dockerfile).Stages
                .Select(stage => stage.Name)
                .ToArray();

            // Filter out any FROM instructions that are based on stage
            IEnumerable<string> fromImages = fromInstructions
                .Where(from => !stageNames.Contains(from.ImageName))
                .Select(from => from.ImageName)
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
                    displayName = $"Nano Server, version {version}";
                }
                else if (os.StartsWith("windowsservercore"))
                {
                    if (version == "ltsc2016")
                    {
                        displayName = "Windows Server Core 2016";
                    }
                    else if (version == "ltsc2019")
                    {
                        displayName = "Windows Server Core 2019";
                    }
                    else
                    {
                        displayName = $"Windows Server Core, version {version}";
                    }
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
                else if (os.Contains("alpine") || os.Contains("centos") || os.Contains("fedora"))
                {
                    int versionIndex = os.IndexOfAny(new char[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' });
                    if (versionIndex != -1)
                    {
                        os = os.Insert(versionIndex, " ");
                    }

                    displayName = os.FirstCharToUpper();
                }
                else
                {
                    throw new NotSupportedException($"The OS version '{os}' is not supported.");
                }
            }

            return displayName;
        }

        public static bool AreMatchingPlatforms(ImageInfo image1, PlatformInfo platform1, ImageInfo image2, PlatformInfo platform2) =>
            platform1.GetUniqueKey(image1) == platform2.GetUniqueKey(image2);

        public string GetUniqueKey(ImageInfo parentImageInfo) =>
            $"{DockerfilePathRelativeToManifest}-{Model.OS}-{Model.OsVersion}-{Model.Architecture}-{parentImageInfo.ProductVersion}";

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
