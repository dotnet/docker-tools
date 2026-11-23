// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder.Models.V2;

public static class ImageArtifactDetailsExtensions
{
    extension(ImageArtifactDetails)
    {
        /// <summary>
        /// Combines disjoint keys from multiple <see cref="ImageArtifactDetails"/>.
        /// </summary>
        /// <throws cref="InvalidDataException">
        /// Thrown if there are duplicate keys in the manifests or indexes.
        /// </throws>
        public static ImageArtifactDetails Merge(IEnumerable<ImageArtifactDetails> inputs)
        {
            ImageArtifactDetails merged = new();
            List<string> errors = [];

            foreach (ImageArtifactDetails input in inputs)
            {
                MergeInto(input.Manifests, merged.Manifests, errors);
                MergeInto(input.Indexes, merged.Indexes, errors);
            }

            if (errors.Count > 0)
            {
                string errorString = string.Join(Environment.NewLine, errors);
                throw new InvalidDataException($"Failed to merge image artifact details:{Environment.NewLine}{errorString}");
            }

            return merged;
        }
    }

    private static void MergeInto<T>(Dictionary<string, T> source, Dictionary<string, T> target, List<string> errors)
    {
        foreach ((string key, T value) in source)
        {
            if (!target.TryAdd(key, value))
            {
                errors.Add($"{typeof(T).Name} '{key}' is defined more than once.");
            }
        }
    }
}
