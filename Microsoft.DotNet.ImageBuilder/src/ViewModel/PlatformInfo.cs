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

        public IDictionary<string, string> BuildArgs { get; private set; }
        public string BuildContextPath { get; private set; }
        public string DockerfilePath { get; private set; }
        public IEnumerable<string> FromImages { get; private set; }
        public Platform Model { get; private set; }
        public IEnumerable<TagInfo> Tags { get; private set; }

        private PlatformInfo()
        {
        }

        public static PlatformInfo Create(Platform model, Manifest manifest, string repoName)
        {
            PlatformInfo platformInfo = new PlatformInfo();
            platformInfo.Model = model;

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
                .Select(kvp => TagInfo.Create(kvp.Key, kvp.Value, manifest, repoName, platformInfo.BuildContextPath))
                .ToArray();

            if (model.BuildArgs == null)
            {
                platformInfo.BuildArgs = ImmutableDictionary<string, string>.Empty;
            }
            else
            {
                platformInfo.BuildArgs = model.BuildArgs;
            }

            platformInfo.InitializeFromImages();

            return platformInfo;
        }

        private void InitializeFromImages()
        {
            string dockerfile = File.ReadAllText(this.DockerfilePath);
            IEnumerable<Match> fromMatches = FromRegex.Matches(dockerfile).Cast<Match>();
            if (!fromMatches.Any())
            {
                throw new InvalidOperationException($"Unable to find a FROM image in {this.DockerfilePath}.");
            }

            FromImages = fromMatches.Select(match => match.Groups["fromImage"].Value).ToArray();
        }
    }
}
