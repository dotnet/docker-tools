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

        public static string GetShortName(this Architecture architecture)
        {
            string shortName;

            switch (architecture)
            {
                case Architecture.AMD64:
                    shortName = "x64";
                    break;
                default:
                    shortName = architecture.ToString().ToLowerInvariant();
                    break;
            }

            return shortName;
        }

        public static string GetNupkgName(this Architecture architecture)
        {
            string nupkgName;

            switch (architecture)
            {
                case Architecture.AMD64:
                    nupkgName = "x64";
                    break;
                case Architecture.ARM:
                    nupkgName = "arm32";
                    break;
                default:
                    nupkgName = architecture.ToString().ToLowerInvariant();
                    break;
            }

            return nupkgName;
        }

        public static string GetDockerName(this Architecture architecture) => architecture.ToString().ToLowerInvariant();

        public static string GetDockerName(this OS os) => os.ToString().ToLowerInvariant();

        public static void Validate(this Manifest manifest, string manifestDirectory)
        {
            if (manifest.Repos == null || !manifest.Repos.Any())
            {
                throw new ValidationException($"The manifest must contain at least one repo.");
            }

            foreach (Repo repo in manifest.Repos)
            {
                ValidateRepo(repo, manifestDirectory);
            }

            ValidateFileReference(manifest.Readme, manifestDirectory);
            ValidateFileReference(manifest.ReadmeTemplate, manifestDirectory);

            if (manifest.ReadmeTemplate != null && manifest.Readme == null)
            {
                throw new ValidationException("The manifest must specify a Readme since a ReadmeTemplate is specified");
            }
        }

        public static string ResolveDockerfilePath(this Platform platform, string manifestDirectory)
        {
            ValidatePathIsRelative(platform.Dockerfile);

            string dockerfilePath = Path.Combine(manifestDirectory, platform.Dockerfile);
            if (File.Exists(dockerfilePath))
            {
                return platform.Dockerfile;
            }
            else
            {
                return Path.Combine(platform.Dockerfile, "Dockerfile");
            }
        }

        public static void ValidateFileReference(string path, string manifestDirectory)
        {
            ValidatePathIsRelative(path);

            if (path != null && !File.Exists(Path.Combine(manifestDirectory, path)))
            {
                throw new FileNotFoundException("Path specified in manifest file does not exist.", path);
            }
        }

        private static void ValidatePathIsRelative(string path)
        {
            if (Path.IsPathRooted(path))
            {
                throw new ValidationException($"Path '{path}' specified in manifest file must be a relative path.");
            }
        }

        private static void ValidateRepo(Repo repo, string manifestDirectory)
        {
            ValidateUniqueTags(repo);
            ValidateFileReference(repo.Readme, manifestDirectory);
            ValidateFileReference(repo.ReadmeTemplate, manifestDirectory);
            ValidateFileReference(repo.McrTagsMetadataTemplate, manifestDirectory);

            if (repo.ReadmeTemplate != null && repo.Readme == null)
            {
                throw new ValidationException($"The repo '{repo.Name}' must specify a Readme since a ReadmeTemplate is specified");
            }
            if (repo.McrTagsMetadataTemplate != null && repo.ReadmeTemplate == null)
            {
                throw new ValidationException($"The repo '{repo.Name}' must specify a ReadmeTemplate since a McrTagsMetadataTemplate is specified");
            }

            foreach (Image image in repo.Images)
            {
                ValidateImage(image, manifestDirectory);
            }
        }

        private static void ValidateImage(Image image, string manifestDirectory)
        {
            foreach (Platform platform in image.Platforms)
            {
                ValidatePlatform(platform, manifestDirectory);
            }
        }

        private static void ValidatePlatform(Platform platform, string manifestDirectory)
        {
            ValidateFileReference(platform.ResolveDockerfilePath(manifestDirectory), manifestDirectory);
            ValidateFileReference(platform.DockerfileTemplate, manifestDirectory);
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
