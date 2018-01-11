// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public static class ModelExtensions
    {
        public static void Validate(this Manifest manifest)
        {
            foreach (Repo repo in manifest.Repos)
            {
                ValidateUniqueTags(repo);
            }
        }

        private static void ValidateUniqueTags(Repo repo)
        {
            IEnumerable<string> sharedTags = repo.Images
                .SelectMany(images => images.SharedTags?.Keys ?? Enumerable.Empty<string>());
            IEnumerable<string> platformTags = repo.Images
                .SelectMany(image => image.Platforms)
                .SelectMany(platform => platform.Tags.Keys);
            IEnumerable<string> allTags = sharedTags
                .Concat(platformTags)
                .ToArray();
            CheckDuplicates(allTags, "tags");

            IEnumerable<string> tagIds = repo.Images
                .SelectMany(image => image.Platforms)
                .SelectMany(platform => platform.Tags)
                .Where(v => v.Value.Id != null)
                .Select(id => id.Value.Id).ToArray();
            CheckDuplicates(tagIds, "IDs");
        }

        private static void CheckDuplicates(IEnumerable<string> source, string type)
        {
            if (source.Count() != source.Distinct().Count())
            {
                IEnumerable<string> duplicates = source
                    .GroupBy(x => x)
                    .Where(x => x.Count() > 1)
                    .Select(x => x.Key);
                throw new Exception(
                    $"Duplicate '{type}' found: {Environment.NewLine}{string.Join(Environment.NewLine, duplicates)}");
            }
        }
    }
}
