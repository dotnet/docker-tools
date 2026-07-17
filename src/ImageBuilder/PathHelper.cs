#nullable disable
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class PathHelper
    {
        public static string NormalizePath(string path) => path.Replace(@"\", "/");

        /// <summary>
        /// Combines two strings into a path. Same as <see cref="Path.Combine"/> but protects against path traversal.
        /// </summary>
        /// <param name="basePath">The base path that the result will remain under.</param>
        /// <param name="paths">Relative segments to append, in order.</param>
        /// <exception cref="ArgumentException">Thrown when path traversal is blocked.</exception>
        public static string SafeCombine(string basePath, params string[] paths)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
            ArgumentNullException.ThrowIfNull(paths);

            string combined = basePath;
            foreach (string relativePath in paths)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

                string platformRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

                if (Path.IsPathRooted(platformRelativePath))
                {
                    throw new ArgumentException(
                        $"Path segment '{relativePath}' must be relative, but it is rooted.",
                        nameof(paths));
                }

                bool hasTraversal = platformRelativePath
                    .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(segment => segment == "..");

                if (hasTraversal)
                {
                    throw new ArgumentException(
                        $"Path segment '{relativePath}' must not contain a '..' traversal component.",
                        nameof(paths));
                }

                combined = Path.Combine(combined, platformRelativePath);
            }

            return combined;
        }

        /// <summary>
        /// Trims the <paramref name="trimPath"/> string from <paramref name="path"/>.
        /// </summary>
        /// <param name="trimPath">The path segment to remove from <paramref name="path"/>.</param>
        /// <param name="path">The path to be trimmed.</param>
        public static string TrimPath(string trimPath, string path)
        {
            if (!NormalizePath(path).StartsWith(NormalizePath(trimPath)))
            {
                throw new InvalidOperationException($"'{path}' must start with '{trimPath}'");
            }
            string result = path.Substring(trimPath.Length);
            if (result.StartsWith("/") || result.StartsWith("\\"))
            {
                result = result.Substring(1);
            }

            return result;
        }

        public static string GetNormalizedDirectory(string path)
        {
            return PathHelper.NormalizePath(Path.GetDirectoryName(path));
        }
    }
}
