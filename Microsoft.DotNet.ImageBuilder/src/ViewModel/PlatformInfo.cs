// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class PlatformInfo
    {
        private const string ArgGroupName = "arg";
        private static readonly string ArgPattern = $"\\$(?<{ArgGroupName}>[\\w\\d-_]+)";
        private const string FromImageMatchName = "fromImage";
        private const string StageIdMatchName = "stageId";
        private static Regex FromRegex { get; } = new Regex($@"FROM\s+(?<{FromImageMatchName}>\S+)(\s+AS\s+(?<{StageIdMatchName}>\S+))?");

        private List<string> _overriddenFromImages = new List<string>();

        public IDictionary<string, string> BuildArgs { get; private set; }
        public string BuildContextPath { get; private set; }
        public string DockerfilePath { get; private set; }
        public IEnumerable<string> ExternalFromImages { get; private set; }
        public IEnumerable<string> IntraRepoFromImages { get; private set; }
        public IEnumerable<string> OverriddenFromImages => _overriddenFromImages;
        public Platform Model { get; private set; }
        private Repo RepoModel { get; set; }
        private string RepoName { get; set; }
        public IEnumerable<TagInfo> Tags { get; private set; }
        private VariableHelper VariableHelper { get; set; }

        private PlatformInfo()
        {
        }

        public static PlatformInfo Create(Platform model, Repo repoModel, string repoName, VariableHelper variableHelper)
        {
            PlatformInfo platformInfo = new PlatformInfo();
            platformInfo.Model = model;
            platformInfo.RepoName = repoName;
            platformInfo.RepoModel = repoModel;
            platformInfo.VariableHelper = variableHelper;

            if (File.Exists(model.Dockerfile))
            {
                platformInfo.DockerfilePath = model.Dockerfile;
                platformInfo.BuildContextPath = Path.GetDirectoryName(model.Dockerfile);
            }
            else
            {
                // Modeled Dockerfile is just the directory containing the "Dockerfile"
                platformInfo.DockerfilePath = Path.Combine(model.Dockerfile, "Dockerfile");
                platformInfo.BuildContextPath = model.Dockerfile;
            }

            platformInfo.Tags = model.Tags
                .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper, platformInfo.BuildContextPath))
                .ToArray();

            platformInfo.InitializeBuildArgs();
            platformInfo.InitializeFromImages();

            return platformInfo;
        }

        private string GetVariableValue(string variableType, string variableName)
        {
            string variableValue = null;

            if (string.Equals(variableType, VariableHelper.SystemVariableTypeId, StringComparison.Ordinal)
                && string.Equals(variableName, VariableHelper.RepoVariableName, StringComparison.Ordinal))
            {
                variableValue = RepoName;
            }

            return variableValue;
        }

        private void InitializeBuildArgs()
        {
            if (Model.BuildArgs == null)
            {
                BuildArgs = ImmutableDictionary<string, string>.Empty;
            }
            else
            {
                BuildArgs = Model.BuildArgs
                    .ToDictionary(kvp => kvp.Key, kvp => VariableHelper.SubstituteValues(kvp.Value, GetVariableValue));
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

            IntraRepoFromImages = fromImages
                .Where(from => from.StartsWith($"{RepoName}:"))
                .ToArray();
            ExternalFromImages = fromImages
                .Except(IntraRepoFromImages)
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
            foreach (Match match in Regex.Matches(instruction, ArgPattern))
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
            if (RepoName != RepoModel.Name && from.StartsWith($"{RepoModel.Name}:"))
            {
                _overriddenFromImages.Add(from);
                from = DockerHelper.ReplaceRepo(from, RepoName);
            }

            return from;
        }
    }
}
