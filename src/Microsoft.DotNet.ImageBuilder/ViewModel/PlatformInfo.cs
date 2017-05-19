// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class PlatformInfo
    {
        private static Regex FromRegex { get; } = new Regex(@"FROM\s+(?<fromImage>\S+)");

        public string FromImage { get; private set; }
        public bool IsExternalFromImage { get; private set; }
        public Platform Model { get; private set; }
        public IEnumerable<string> Tags { get; private set; }

        private PlatformInfo()
        {
        }

        public static PlatformInfo Create(Platform model, Manifest manifest, string repoName)
        {
            PlatformInfo platformInfo = new PlatformInfo();
            platformInfo.Model = model;
            platformInfo.InitializeFromImage();
            platformInfo.IsExternalFromImage = !platformInfo.FromImage.StartsWith($"{repoName}:");
            platformInfo.Tags = model.Tags
                .Select(tag => $"{repoName}:{manifest.SubstituteTagVariables(tag)}")
                .ToArray();

            return platformInfo;
        }

        private void InitializeFromImage()
        {
            Match fromMatch = FromRegex.Match(File.ReadAllText(Path.Combine(Model.Dockerfile, "Dockerfile")));
            if (!fromMatch.Success)
            {
                throw new InvalidOperationException($"Unable to find the FROM image in {Model.Dockerfile}.");
            }

            FromImage = fromMatch.Groups["fromImage"].Value;
        }
    }
}
