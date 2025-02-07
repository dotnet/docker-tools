// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class FileHelper
    {
        public static void ForceDeleteDirectory(string path)
        {
            // Handles read-only dirs/files by forcing them to be writable

            if (!Directory.Exists(path))
            {
                return;
            }

            string[] files = Directory.GetFiles(path);
            string[] directories = new DirectoryInfo(path).GetDirectories()
                .Where(dir => dir.LinkTarget is null) // Ignore symlinks
                .Select(dir => dir.FullName)
                .ToArray();

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in directories)
            {
                ForceDeleteDirectory(dir);
            }

            File.SetAttributes(path, FileAttributes.Normal);

            Directory.Delete(path, true);
        }

        public static void CreateDirectoryIfNotExists(string path, bool throwIfNotEmpty = false)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                return;
            }

            if (throwIfNotEmpty && Directory.EnumerateFileSystemEntries(path).Any())
            {
                throw new IOException($"Directory '{path}' already exists and is not empty");
            }
        }
    }
}
