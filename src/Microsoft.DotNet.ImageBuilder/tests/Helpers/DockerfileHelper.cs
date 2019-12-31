// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class DockerfileHelper
    {
        public static void CreateDockerfile(string destinationFolder, string fromTag)
        {
            DirectoryInfo dir = Directory.CreateDirectory(destinationFolder);
            string dockerfilePath = Path.Combine(dir.FullName, "Dockerfile");
            File.WriteAllText(dockerfilePath, "FROM base");
        }
    }
}
