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

        public IDictionary<string, string> BuildArgs { get; private set; }
        public string BuildContextPath { get; private set; }
        public string DockerfilePath { get; private set; }
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
            PlatformInfo platformInfo = new PlatformInfo(baseDirectory);
            platformInfo.Model = model;
            platformInfo.RepoName = repoName;
            platformInfo.FullRepoModelName = fullRepoModelName;
            platformInfo.VariableHelper = variableHelper;

            // Ensure that we construct an absolute path here to check for the file. Using a relative path
            // has a dependency on the current working directory which is set for the entire process and
            // is incompatible with the parallelism that is used in the GetStaleImagesCommand.  That command
            // processes multiple manifests in varying directories in parallel, so relying on current working
            // directory is not possible in that case.
            if (File.Exists(Path.Combine(baseDirectory, model.Dockerfile)))
            {
                platformInfo.DockerfilePath = model.Dockerfile;
                platformInfo.BuildContextPath = Path.GetDirectoryName(Path.Combine(baseDirectory, model.Dockerfile));
            }
            else
            {
                // Modeled Dockerfile is just the directory containing the "Dockerfile"
                platformInfo.DockerfilePath = Path.Combine(baseDirectory, model.Dockerfile, "Dockerfile");
                platformInfo.BuildContextPath = model.Dockerfile;
            }

            platformInfo.Tags = model.Tags
                .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper, platformInfo.BuildContextPath))
                .ToArray();

            return platformInfo;
        }

        public void Initialize(IEnumerable<string> internalRepos, string registry)
        {
            InitializeBuildArgs();
            InitializeFromImages(internalRepos);

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

        private void InitializeFromImages(IEnumerable<string> internalRepos)
        {
            _overriddenFromImages = new List<string>();

            string dockerfile = File.ReadAllText(Path.Combine(this.baseDirectory, DockerfilePath));
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

            InternalFromImages = fromImages
                .Where(from => internalRepos.Any(repo => from.StartsWith($"{repo}:")))
                .ToArray();
            ExternalFromImages = fromImages
                .Except(InternalFromImages)
                .ToArray();
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
