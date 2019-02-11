// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Model;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public static class ModelExtensions
    {
        public static string GetDisplayName(this Architecture architecture, string variant = null)
        {
            string displayName;

            switch (architecture)
            {
                case Architecture.ARM:
                    displayName = "arm32";
                    break;
                default:
                    displayName = architecture.ToString().ToLowerInvariant();
                    break;
            }

            if (variant != null)
            {
                displayName += variant.ToLowerInvariant();
            }

            return displayName;
        }

        public static void Validate(this Manifest manifest)
        {
            foreach (Repo repo in manifest.Repos)
            {
                ValidateUniqueTags(repo);
            }
        }

        private static void ValidateUniqueTags(Repo repo)
        {
            IEnumerable<KeyValuePair<string, Tag>> sharedTags = repo.Images
                .SelectMany(images => images.SharedTags ?? Enumerable.Empty<KeyValuePair<string, Tag>>());
            IEnumerable<KeyValuePair<string, Tag>> platformTags = repo.Images
                .SelectMany(image => image.Platforms)
                .SelectMany(platform => platform.Tags);
            IEnumerable<KeyValuePair<string, Tag>> allTags = sharedTags
                .Concat(platformTags)
                .ToArray();

            IEnumerable<string> tagNames = allTags
                .Select(kvp => kvp.Key)
                .ToArray();
            ValidateUniqueElements(tagNames, "tags");

            IEnumerable<string> tagIds = platformTags
                .Select(kvp => kvp.Value.Id)
                .Where(id => id != null)
                .ToArray();
            ValidateUniqueElements(tagIds, "tag IDs");
        }

        private static void ValidateUniqueElements(IEnumerable<string> source, string elementsDescription)
        {
            if (source.Count() != source.Distinct().Count())
            {
                IEnumerable<string> duplicates = source
                    .GroupBy(x => x)
                    .Where(x => x.Count() > 1)
                    .Select(x => x.Key);
                throw new ValidationException(
                    $"Duplicate {elementsDescription} found: {Environment.NewLine}{string.Join(Environment.NewLine, duplicates)}");
            }
        }
    }
}
