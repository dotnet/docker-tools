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

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class PlatformInfo
    {
        private const string ArgGroupName = "arg";
        private const string FromImageMatchName = "fromImage";
        private const string StageIdMatchName = "stageId";
        private static Regex FromRegex { get; } = new Regex($@"FROM\s+(?<{FromImageMatchName}>\S+)(\s+AS\s+(?<{StageIdMatchName}>\S+))?");

        private static readonly string s_argPattern = $"\\$(?<{ArgGroupName}>[\\w\\d-_]+)";

        private readonly string baseDirectory;
        private List<string> _overriddenFromImages;
        private IEnumerable<string> internalRepos;

        public IDictionary<string, string> BuildArgs { get; private set; }
        public string BuildContextPath { get; private set; }
        public string DockerfilePath { get; private set; }
        public string DockerfilePathRelativeToManifest { get; private set; }
        public IEnumerable<string> FromImages { get; private set; }
        public IEnumerable<string> ExternalFromImages { get; private set; }
        public IEnumerable<string> InternalFromImages { get; private set; }
        public Platform Model { get; private set; }
        public IEnumerable<string> OverriddenFromImages { get => _overriddenFromImages; }
        private string FullRepoModelName { get; set; }
        private string RepoName { get; set; }
        public IEnumerable<TagInfo> Tags { get; private set; }
        public IDictionary<string, CustomBuildLegGroupingInfo> CustomLegGroupings { get; private set; }
        private VariableHelper VariableHelper { get; set; }

        private PlatformInfo(string baseDirectory)
        {
            this.baseDirectory = baseDirectory;
        }

        public static PlatformInfo Create(Platform model, string fullRepoModelName, string repoName, VariableHelper variableHelper, string baseDirectory)
        {
            PlatformInfo platformInfo = new PlatformInfo(baseDirectory)
            {
                Model = model,
                RepoName = repoName,
                FullRepoModelName = fullRepoModelName,
                VariableHelper = variableHelper
            };

            string dockerfileWithBaseDir = Path.Combine(baseDirectory, model.ResolveDockerfilePath(baseDirectory));

            platformInfo.DockerfilePath = PathHelper.NormalizePath(dockerfileWithBaseDir);
            platformInfo.BuildContextPath = PathHelper.NormalizePath(Path.GetDirectoryName(dockerfileWithBaseDir));
            platformInfo.DockerfilePathRelativeToManifest = PathHelper.TrimPath(baseDirectory, platformInfo.DockerfilePath);

            platformInfo.Tags = model.Tags
                .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper, platformInfo.BuildContextPath))
                .ToArray();

            return platformInfo;
        }

        public void Initialize(IEnumerable<string> internalRepos, string registry)
        {
            this.internalRepos = internalRepos;
            InitializeBuildArgs();
            InitializeFromImages();

            CustomLegGroupings = this.Model.CustomBuildLegGrouping
                .Select(grouping =>
                    new CustomBuildLegGroupingInfo(
                        grouping.Name,
                        grouping.Dependencies
                            .Select(d => VariableHelper.SubstituteValues(d))
                            .ToArray()))
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

            string dockerfile = File.ReadAllText(DockerfilePath);
            IList<Match> fromMatches = FromRegex.Matches(dockerfile);

            if (!fromMatches.Any())
            {
                throw new InvalidOperationException($"Unable to find a FROM image in {DockerfilePath}.");
            }

            FromImages = fromMatches
                .Select(match => match.Groups[FromImageMatchName].Value)
                .Select(from => SubstituteOverriddenRepo(from))
                .Select(from => SubstituteBuildArgs(from))
                .Where(from => !IsStageReference(from, fromMatches))
                .ToArray();

            InternalFromImages = FromImages
                .Where(from => IsInternalFromImage(from))
                .ToArray();
            ExternalFromImages = FromImages
                .Except(InternalFromImages)
                .ToArray();
        }

        public bool IsInternalFromImage(string fromImage)
        {
            return this.internalRepos.Any(repo => fromImage.StartsWith($"{repo}:"));
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
                if (!BuildArgs.TryGetValue(match.Groups[ArgGroupName].Value, out string argValue))
                {
                    throw new InvalidOperationException($"A value was not found for the ARG '{match.Value}'");
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
