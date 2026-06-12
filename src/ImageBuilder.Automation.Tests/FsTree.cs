// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// Reads and writes the contents of a working tree as a flat map of
/// '/'-separated relative paths to file contents.
/// </summary>
internal static class FsTree
{
    public static void Write(string root, IReadOnlyDictionary<string, string> tree)
    {
        foreach ((string path, string content) in tree)
        {
            string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }
    }

    public static ImmutableDictionary<string, string> Read(string root) =>
        Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToImmutableDictionary(
                file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'),
                File.ReadAllText
            );

    public static bool TreesEqual(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(kvp => b.TryGetValue(kvp.Key, out string? value) && value == kvp.Value);
}
