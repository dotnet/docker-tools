// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class DockerfileHelper
    {
        public static string CreateDockerfile(string relativeDirectory, TempFolderContext context, string fromTag = "base")
        {
            Directory.CreateDirectory(Path.Combine(context.Path, relativeDirectory));
            string dockerfileRelativePath = Path.Combine(relativeDirectory, "Dockerfile");
            File.WriteAllText(PathHelper.NormalizePath(Path.Combine(context.Path, dockerfileRelativePath)), $"FROM {fromTag}");
            return PathHelper.NormalizePath(dockerfileRelativePath);
        }
    }
}
