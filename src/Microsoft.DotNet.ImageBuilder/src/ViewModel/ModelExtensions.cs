// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

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

        public static string GetDockerName(this Architecture architecture) => architecture.ToString().ToLowerInvariant();

        public static string GetDockerName(this OS os) => os.ToString().ToLowerInvariant();

        public static void Validate(this Manifest manifest, string manifestDirectory)
        {
            foreach (Repo repo in manifest.Repos)
            {
                ValidateUniqueTags(repo);

                if (Path.IsPathRooted(repo.ReadmePath))
                {
                    throw new ValidationException($"Readme path '{repo.ReadmePath}' for repo {repo.Name} must be a relative path.");
                }

                EnsureFileExists(repo.ReadmePath, manifestDirectory);

                if (Path.IsPathRooted(repo.McrTagsMetadataTemplatePath))
                {
                    throw new ValidationException($"Tags template path '{repo.McrTagsMetadataTemplatePath}' for repo {repo.Name} must be a relative path.");
                }

                EnsureFileExists(repo.McrTagsMetadataTemplatePath, manifestDirectory);
            }

            if (Path.IsPathRooted(manifest.ReadmePath))
            {
                throw new ValidationException($"Readme path '{manifest.ReadmePath}' must be a relative path.");
            }

            EnsureFileExists(manifest.ReadmePath, manifestDirectory);
        }

        private static void EnsureFileExists(string path, string manifestDirectory)
        {
            if (path != null && !File.Exists(Path.Combine(manifestDirectory, path)))
            {
                throw new FileNotFoundException("Path specified in manifest file does not exist.", path);
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
