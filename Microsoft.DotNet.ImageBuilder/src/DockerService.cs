﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IDockerService))]
    internal class DockerService : IDockerService
    {
        public string GetImageDigest(string image, bool isDryRun)
        {
            return DockerHelper.GetImageDigest(image, isDryRun);
        }

        public void PullImage(string image, bool isDryRun)
        {
            DockerHelper.PullImage(image, isDryRun);
        }
    }
}
