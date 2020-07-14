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
            string relativeDockerfilePath = PathHelper.NormalizePath(Path.Combine(relativeDirectory, "Dockerfile"));
            CreateFile(relativeDockerfilePath, context, $"FROM {fromTag}");
            return relativeDockerfilePath;
        }

        public static void CreateFile(string relativeFileName, TempFolderContext context, string content)
        {
            string fullFilePath = Path.Combine(context.Path, relativeFileName);
            Directory.CreateDirectory(Directory.GetParent(fullFilePath).FullName);
            File.WriteAllText(fullFilePath, content);
        }
    }
}
