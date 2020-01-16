// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class PathHelper
    {
        public static string NormalizePath(string path) => path.Replace(@"\", "/");

        public static string StripBaseDirectory(string baseDirectory, string path)
        {
            Debug.Assert(NormalizePath(path).StartsWith(NormalizePath(baseDirectory)));
            string result = path.Substring(baseDirectory.Length);
            if (result.StartsWith("/") || result.StartsWith("\\"))
            {
                result = result.Substring(1);
            }

            return result;
        }

        public static string GetBaseDirectory(string manifestPath)
        {
            return PathHelper.NormalizePath(Path.GetDirectoryName(manifestPath));
        }
    }
}
