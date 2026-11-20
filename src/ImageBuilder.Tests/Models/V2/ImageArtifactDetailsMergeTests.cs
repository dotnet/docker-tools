// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsCheck;
using Microsoft.DotNet.ImageBuilder.Models.V2;

namespace Microsoft.DotNet.ImageBuilder.Tests.Models.V2;

[TestClass]
public sealed class ImageArtifactDetailsMergeTests
{
    // Keys use only the letters a to d, so the failing cases that CsCheck shrinks are short.
    private static readonly Gen<string> GenKey = Gen.String[Gen.Char["abcd"], 1, 4];

    private static readonly Gen<string> GenDigest = Gen.UInt.Select(n => $"sha256:{n:x64}");

    private static readonly Gen<ImageManifest> GenImageManifest =
        from digest in GenDigest
        select new ImageManifest { Registry = "registry", Repository = "repo", Digest = digest };

    private static Gen<ImageIndex> GenImageIndex(string[] manifestDigests) =>
        from digest in GenDigest
        from manifests in Gen.Shuffle(manifestDigests, 0, manifestDigests.Length)
        select new ImageIndex { Registry = "registry", Repository = "repo", Digest = digest, Manifests = manifests };

    // Generates a valid ImageArtifactDetails. Each of its indexes lists only digests of its own
    // manifests.
    private static readonly Gen<ImageArtifactDetails> GenValidImageArtifactDetails =
        from manifests in Gen.Dictionary(GenKey, GenImageManifest)[0, 5]
        let manifestDigests = manifests.Values.Select(manifest => manifest.Digest).ToArray()
        from indexes in Gen.Dictionary(GenKey, GenImageIndex(manifestDigests))[0, 3]
        select new ImageArtifactDetails { Manifests = manifests, Indexes = indexes };

    // Generates 1 to 5 valid Inputs with disjoint keys. Expected contains every manifest and index
    // from Inputs. Each key starts with its input's position in Inputs, so two inputs can't have
    // the same key.
    private static readonly Gen<DisjointScenario> GenDisjointScenario =
        from inputs in GenValidImageArtifactDetails.Array[1, 5]
        let disjointInputs = inputs.Select((input, i) => WithKeyPrefix(input, $"{i}/")).ToArray()
        select new DisjointScenario(
            Expected: new ImageArtifactDetails
            {
                Manifests = disjointInputs.SelectMany(input => input.Manifests).ToDictionary(),
                Indexes = disjointInputs.SelectMany(input => input.Indexes).ToDictionary(),
            },
            Inputs: disjointInputs);

    // Generates a disjoint set of ImageArtifactDetails, but with one of the elements duplicated.
    private static readonly Gen<ImageArtifactDetails[]> GenInputsWithDuplicateKeys =
        from scenario in GenDisjointScenario.Where(scenario => scenario.Inputs.Any(IsNotEmpty))
        from duplicated in Gen.OneOfConst(scenario.Inputs.Where(IsNotEmpty).ToArray())
        from inputs in Gen.Shuffle((ImageArtifactDetails[])[.. scenario.Inputs, Copy(duplicated)])
        select inputs;

    /// <summary>
    /// For all ImageArtifactDetails d1, ..., dn with disjoint keys,
    /// Merge(d1, ..., dn) contains exactly the manifests and indexes of d1, ..., dn.
    /// </summary>
    [TestMethod]
    public void Merge_DisjointInputs_ContainsExactlyTheInputContent()
    {
        GenDisjointScenario.Sample(scenario =>
            AreEquivalent(ImageArtifactDetails.Merge(scenario.Inputs), scenario.Expected));
    }

    /// <summary>
    /// For all ImageArtifactDetails d1, ..., dn where some key appears in more than one di,
    /// Merge(d1, ..., dn) throws, and the error message names each such key.
    /// </summary>
    [TestMethod]
    public void Merge_DuplicateKeys_ReportsEveryDuplicate()
    {
        GenInputsWithDuplicateKeys.Sample(inputs =>
        {
            IEnumerable<string> duplicateKeys =
            [
                .. DuplicateKeys(inputs.Select(input => input.Manifests.Keys)),
                .. DuplicateKeys(inputs.Select(input => input.Indexes.Keys)),
            ];

            try
            {
                ImageArtifactDetails.Merge(inputs);
                return false;
            }
            catch (InvalidDataException exception)
            {
                return duplicateKeys.All(key => exception.Message.Contains($"'{key}'", StringComparison.Ordinal));
            }
        });
    }

    private static ImageArtifactDetails WithKeyPrefix(ImageArtifactDetails details, string prefix) =>
        new()
        {
            Manifests = details.Manifests.ToDictionary(pair => prefix + pair.Key, pair => pair.Value),
            Indexes = details.Indexes.ToDictionary(pair => prefix + pair.Key, pair => pair.Value),
        };

    private static bool IsNotEmpty(ImageArtifactDetails details) =>
        details.Manifests.Count > 0 || details.Indexes.Count > 0;

    private static ImageArtifactDetails Copy(ImageArtifactDetails details) =>
        new() { Manifests = new(details.Manifests), Indexes = new(details.Indexes) };

    private static IEnumerable<string> DuplicateKeys(IEnumerable<IEnumerable<string>> keysPerInput) =>
        keysPerInput
            .SelectMany(keys => keys)
            .GroupBy(key => key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

    private static bool AreEquivalent(ImageArtifactDetails a, ImageArtifactDetails b) =>
        a.Manifests.ToHashSet().SetEquals(b.Manifests) && a.Indexes.ToHashSet().SetEquals(b.Indexes);

    /// <param name="Expected">Every manifest and index from <paramref name="Inputs"/>.</param>
    /// <param name="Inputs">ImageArtifactDetails with disjoint keys.</param>
    private sealed record DisjointScenario(ImageArtifactDetails Expected, ImageArtifactDetails[] Inputs);
}
