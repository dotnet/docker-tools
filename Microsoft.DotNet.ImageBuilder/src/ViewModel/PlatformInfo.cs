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
        private static Regex FromRegex { get; } = new Regex(@"FROM\s+(?<fromImage>\S+)");

        public string BuildContextPath { get; private set; }
        public string DockerfilePath { get; private set; }
        public IEnumerable<string> FromImages { get; private set; }
        public Platform Model { get; private set; }
        public IEnumerable<TagInfo> Tags { get; private set; }
        private VariableHelper VariableHelper { get; set; }

        private PlatformInfo()
        {
        }

        public static PlatformInfo Create(Platform model, string repoName, VariableHelper variableHelper)
        {
            PlatformInfo platformInfo = new PlatformInfo();
            platformInfo.Model = model;
            platformInfo.VariableHelper = variableHelper;

            if (File.Exists(model.Dockerfile))
            {
                platformInfo.DockerfilePath = model.Dockerfile;
                platformInfo.BuildContextPath = Path.GetDirectoryName(model.Dockerfile);
            }
            else
            {
                // Modeled Dockefile is just the directory containing the "Dockerfile"
                platformInfo.DockerfilePath = Path.Combine(model.Dockerfile, "Dockerfile");
                platformInfo.BuildContextPath = model.Dockerfile;
            }

            platformInfo.Tags = model.Tags
                .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, repoName, variableHelper, platformInfo.BuildContextPath))
                .ToArray();

            platformInfo.InitializeFromImages();

            return platformInfo;
        }

        public IDictionary<string, string> GetBuildArgs()
        {
            IDictionary<string, string> buildArgs;

            if (Model.BuildArgs == null)
            {
                buildArgs = ImmutableDictionary<string, string>.Empty;
            }
            else
            {
                buildArgs = Model.BuildArgs
                    .ToDictionary(kvp => kvp.Key, kvp => VariableHelper.SubstituteValues(kvp.Value));
            }

            return buildArgs;
        }

        private void InitializeFromImages()
        {
            string dockerfile = File.ReadAllText(this.DockerfilePath);
            IEnumerable<Match> fromMatches = FromRegex.Matches(dockerfile);

            if (!fromMatches.Any())
            {
                throw new InvalidOperationException($"Unable to find a FROM image in {this.DockerfilePath}.");
            }

            FromImages = fromMatches
                .Select(match => match.Groups["fromImage"].Value)
                .Where(from => !from.Contains("$"))
                .ToArray();
        }
    }
}
