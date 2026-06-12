// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// A generatable, data-only stand-in for an <see cref="ApplyChanges"/>
/// callback: delete the given paths, then write the given files. Because it
/// writes fixed contents, it is idempotent on trees (delta ∘ delta == delta),
/// which is the contract <see cref="ApplyChanges"/> implementations must
/// satisfy. <see cref="ApplyTo"/> is the pure denotation that properties use
/// to compute expected results; <see cref="ToApplyChanges"/> is the effectful
/// version handed to the library.
/// </summary>
internal sealed record TreeDelta(ImmutableSortedDictionary<string, string> Writes, ImmutableSortedSet<string> Deletes)
{
    public ImmutableDictionary<string, string> ApplyTo(ImmutableDictionary<string, string> tree) =>
        tree.RemoveRange(Deletes).SetItems(Writes);

    public ApplyChanges ToApplyChanges() =>
        repoRoot =>
        {
            foreach (string path in Deletes)
            {
                string fullPath = Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }

            FsTree.Write(repoRoot, Writes);
            return Task.CompletedTask;
        };

    public override string ToString() =>
        $"Δ(write: [{string.Join(", ", Writes.Select(kvp => $"{kvp.Key}={kvp.Value.Replace("\n", "\\n")}"))}], "
        + $"delete: [{string.Join(", ", Deletes)}])";
}
