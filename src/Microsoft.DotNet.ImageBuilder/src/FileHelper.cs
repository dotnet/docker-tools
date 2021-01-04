// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class FileHelper
    {
        public static void ForceDeleteDirectory(string path)
        {
            // Handles read-only dirs/files by forcing them to be writable

            DirectoryInfo directory = new DirectoryInfo(path)
            {
                Attributes = FileAttributes.Normal
            };

            Parallel.ForEach(directory.GetFileSystemInfos("*", SearchOption.AllDirectories), fileInfo =>
            {
                fileInfo.Attributes = FileAttributes.Normal;
            });

            directory.Delete(true);
        }
    }
}
