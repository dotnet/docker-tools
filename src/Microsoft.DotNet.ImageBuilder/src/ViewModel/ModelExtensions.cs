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
            string displayName = architecture switch
            {
                Architecture.ARM => "arm32",
                _ => architecture.ToString().ToLowerInvariant(),
            };

            if (variant != null)
            {
                displayName += variant.ToLowerInvariant();
            }

            return displayName;
        }

        public static string GetShortName(this Architecture architecture)
        {
            return architecture switch
            {
                Architecture.AMD64 => "x64",
                _ => architecture.ToString().ToLowerInvariant(),
            };
        }

        public static string GetNupkgName(this Architecture architecture)
        {
            return architecture switch
            {
                Architecture.AMD64 => "x64",
                Architecture.ARM => "arm32",
                _ => architecture.ToString().ToLowerInvariant(),
            };
        }

        public static string GetDockerName(this Architecture architecture) => architecture.ToString().ToLowerInvariant();

        public static string GetDockerName(this OS os) => os.ToString().ToLowerInvariant();

        public static void Validate(this Manifest manifest, string manifestDirectory)
        {
            if (manifest.Repos == null || !manifest.Repos.Any())
            {
                throw new ValidationException($"The manifest must contain at least one repo.");
            }

            ValidateReadmeFilenames(manifest);

            foreach (Repo repo in manifest.Repos)
            {
                ValidateRepo(repo, manifestDirectory);
            }

            if (manifest.Readme is not null)
            {
                ValidateFileReference(manifest.Readme.Path, manifestDirectory);
                ValidateFileReference(manifest.Readme.TemplatePath, manifestDirectory);
            }
        }

        private static void ValidateReadmeFilenames(Manifest manifest)
        {
            // Readme filenames must be unique across all the readmes regardless of their path.
            // This is because they will eventually be published to mcrdocs where all of the readmes are contained within the same directory

            IEnumerable<IGrouping<string, string>> readmePathsWithDuplicateFilenames = manifest.Repos
                .SelectMany(repo => repo.Readmes.Select(readme => readme.Path))
                .GroupBy(readmePath => Path.GetFileName(readmePath))
                .Where(group => group.Count() > 1);

            if (readmePathsWithDuplicateFilenames.Any())
            {
                IEnumerable<string> errorMessages = readmePathsWithDuplicateFilenames
                    .Select(group =>
                        "Readme filenames must be unique, regardless of the directory path. " +
                        "The following readme paths have filenames that conflict with each other:" +
                        Environment.NewLine +
                        string.Join(Environment.NewLine, group.ToArray()));

                throw new ValidationException(string.Join(Environment.NewLine + Environment.NewLine, errorMessages.ToArray()));
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
            ValidateFileReference(repo.McrTagsMetadataTemplate, manifestDirectory);

            foreach (Readme readme in repo.Readmes)
            {
                ValidateFileReference(readme.Path, manifestDirectory);
                ValidateFileReference(readme.TemplatePath, manifestDirectory);

                if (repo.McrTagsMetadataTemplate != null && readme.TemplatePath == null)
                {
                    throw new ValidationException($"The repo '{repo.Name}' must specify a ReadmeTemplate since a McrTagsMetadataTemplate is specified");
                }
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
