#nullable disable
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class DockerfileHelper
    {
        public static string CreateDockerfile(string relativeDirectory, TempFolderContext context, string fromTag1 = "base", string fromTag2 = null)
        {
            string relativeDockerfilePath = PathHelper.NormalizePath(Path.Combine(relativeDirectory, "Dockerfile"));
            string contents = $"FROM {fromTag1}";
            if (fromTag2 is not null)
            {
                contents += $"{Environment.NewLine}FROM {fromTag2}";
            }
            CreateFile(relativeDockerfilePath, context, contents);
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
