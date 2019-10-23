// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IDockerService))]
    internal class DockerService : IDockerService
    {
        public Architecture Architecture => DockerHelper.Architecture;

        public string GetImageDigest(string image, bool isDryRun)
        {
            return DockerHelper.GetImageDigest(image, isDryRun);
        }

        public void PullImage(string image, bool isDryRun)
        {
            DockerHelper.PullImage(image, isDryRun);
        }

        public string PushImage(string tag, bool isDryRun)
        {
            return DockerHelper.PushImage(tag, isDryRun);
        }

        public void BuildImage(string dockerfilePath, string buildContextPath, IEnumerable<string> tags, IDictionary<string, string> buildArgs, bool isRetryEnabled, bool isDryRun)
        {
            string tagArgs = $"-t {string.Join(" -t ", tags)}";

            IEnumerable<string> buildArgList = buildArgs
                .Select(buildArg => $" --build-arg {buildArg.Key}={buildArg.Value}");
            string buildArgsString = String.Join(string.Empty, buildArgList);

            string dockerArgs = $"build {tagArgs} -f {dockerfilePath}{buildArgsString} {buildContextPath}";

            if (isRetryEnabled)
            {
                ExecuteHelper.ExecuteWithRetry("docker", dockerArgs, isDryRun);
            }
            else
            {
                ExecuteHelper.Execute("docker", dockerArgs, isDryRun);
            }
        }
    }
}
