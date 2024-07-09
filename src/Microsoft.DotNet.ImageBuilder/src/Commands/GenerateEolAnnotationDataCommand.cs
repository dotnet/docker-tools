// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Annotations;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Newtonsoft.Json;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    [Export(typeof(ICommand))]
    public class GenerateEolAnnotationDataCommand : Command<GenerateEolAnnotationDataOptions, GenerateEolAnnotationDataOptionsBuilder>
    {
        private readonly IAzureLogService _azureLogService;
        private readonly IDotNetReleasesService _dotNetReleasesService;
        private readonly ILoggerService _loggerService;
        private readonly DateOnly _eolDate;
        private readonly Dictionary<string, DateOnly?> _digestsToAnnotate = [];

        private Dictionary<string, DateOnly?> _productEolDates = null!;
        private ImageArtifactDetails _oldImageArtifactDetails = null!;
        private ImageArtifactDetails _newImageArtifactDetails = null!;

        [ImportingConstructor]
        public GenerateEolAnnotationDataCommand(
            IAzureLogService azureLogService,
            IDotNetReleasesService dotNetReleasesService,
            ILoggerService loggerService)
            : base()
        {
            _azureLogService = azureLogService ?? throw new ArgumentNullException(nameof(azureLogService));
            _dotNetReleasesService = dotNetReleasesService ?? throw new ArgumentNullException(nameof(dotNetReleasesService));
            _loggerService = loggerService ?? throw new ArgumentNullException(nameof(loggerService));

            _eolDate = DateOnly.FromDateTime(DateTime.UtcNow); // default EOL date
        }

        protected override string Description => "Generate EOL annotation data";

        public override async Task ExecuteAsync()
        {
            _productEolDates = await _dotNetReleasesService.GetProductEolDatesFromReleasesJson();

            _oldImageArtifactDetails = LoadImageInfoData(Options.OldImageInfoPath);
            _newImageArtifactDetails = LoadImageInfoData(Options.NewImageInfoPath);

            DiscoverAllDigestsForAnnotation();

            SerializeDigestDataJson();
        }

        private void SerializeDigestDataJson()
        {
            EolAnnotationsData eolAnnotations = new([], _eolDate);
            foreach (KeyValuePair<string, DateOnly?> digest in _digestsToAnnotate)
            {
                eolAnnotations.EolDigests.Add(new EolDigestData(digest.Key) { EolDate = digest.Value });
            }

            string annotationsJson = JsonConvert.SerializeObject(eolAnnotations, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            File.WriteAllText(Options.EolDigestsListPath, annotationsJson);
        }

        public void DiscoverAllDigestsForAnnotation()
        {
            try
            {
                foreach (RepoData oldRepo in _oldImageArtifactDetails.Repos)
                {
                    RepoData? newRepo = _newImageArtifactDetails.Repos.FirstOrDefault(r => r.Repo == oldRepo.Repo);
                    if (newRepo == null)
                    {
                        // Annotate all images in the old repo as EOL
                        AddRepoForAnnotation(oldRepo);
                        continue;
                    }

                    foreach (ImageData oldImage in oldRepo.Images)
                    {
                        // Logic:
                        // For each platform in the old image, check if it exists in the new repo,
                        // where the platform is defined by Dockerfile value.
                        // If it doesn't, add for annotation.
                        // If none of the platforms, in this image, exist in new image, annotate the image as EOL

                        ImageData? newImage = null;
                        string oldImageIdentity = ImageIdentityString(oldImage);
                        foreach (PlatformData oldPlatform in oldImage.Platforms)
                        {
                            // There might be more than one image that contains the platform entry for this dockerfile
                            // find the correct one that matches product version and the set of image tags
                            newImage ??= newRepo!.Images
                                .Where(i => i.Platforms.Any(p => p.Dockerfile == oldPlatform.Dockerfile))
                                .FirstOrDefault(i => ImageIdentityString(i) == oldImageIdentity);

                            if (newImage == null)
                            {
                                AddDigestForAnnotation(oldPlatform.Digest, oldPlatform.SimpleTags.First());
                            }
                            else
                            {
                                PlatformData? newPlatform = newImage.Platforms.FirstOrDefault(p => p.Dockerfile == oldPlatform.Dockerfile);
                                if (newPlatform == null || oldPlatform.Digest != newPlatform.Digest)
                                {
                                    AddDigestForAnnotation(oldPlatform.Digest, oldPlatform.SimpleTags.First(), newDigest: newPlatform?.Digest);
                                }
                            }
                        }

                        // If we didn't find the new image that contained any of the Dockerfiles from the old image,
                        // or if new image manifest digest is different from old image manifest digest,
                        // annotate old image manifest digest.
                        if (oldImage.Manifest != null &&
                            (newImage == null ||
                             newImage.Manifest == null ||
                             oldImage.Manifest.Digest != newImage.Manifest.Digest))
                        {
                            AddDigestForAnnotation(oldImage.Manifest.Digest, oldImage.Manifest.SharedTags.First(), newDigest: newImage?.Manifest?.Digest);
                        }
                    }
                }

                if (Options.AnnotateEolProducts)
                {
                    // Annotate images for eol products in new image info
                    foreach (ImageData image in _newImageArtifactDetails.Repos.SelectMany(repo => repo.Images))
                    {
                        AnnotateImageIfProductIsEol(image);
                    }
                }
            }
            catch (Exception e)
            {
                _loggerService.WriteError($"Error occurred while generating EOL annotation data: {e}");
                throw;
            }
        }

        private void AnnotateDanglingDigests(string oldDigest, string tag, DateOnly? eolDate = null, string? newDigest = null)
        {
            // If newDigest is null, annotate all dangling digests starting with oldDigest.
            // newDigest can be null if image or platform is being removed.
            // If newDigest is not null, annotate all dangling digests starting with oldDigest, except newDigest.

            string[] oldDigestParts = oldDigest.Split('@');
            string repo = oldDigestParts[0].Replace("mcr.microsoft.com/", Options.RepoPrefix);

            List<AcrEventEntry> recentPushes = _azureLogService.GetRecentPushEntries(repo, tag, Options.LogsWorkspaceId, Options.LogsQueryDayRange).Result;
            if (recentPushes.Count == 0)
            {
                _loggerService.WriteMessage($"No recent pushes found for {repo}:{tag}");
                return;
            }

            // We might not find the old digest in recent pushes, and that is OK.
            // We will annotate all digests, except the new one.
            int oldIndex = recentPushes.FindLastIndex(e => e.Digest == oldDigestParts[1]);

            int newIndex = recentPushes.FindIndex(e => e.Digest == newDigest?.Split('@')[1]);
            if (newIndex == -1)
            {
                newIndex = recentPushes.Count;
            }

            for (int i = oldIndex + 1; i < newIndex; i++)
            {
                _digestsToAnnotate.Add(oldDigestParts[0] + "@" + recentPushes[i].Digest, eolDate);
            }
        }

        private void AddDigestForAnnotation(string digest, string tag, DateOnly? eolDate = null, string? newDigest = null, bool isNewDigest = false)
        {
            // TODO: This could likely use some updates. 'digest' can represent either old or new one
            // as indicated by 'isNewDigest' flag. This is not clear from the method signature.
            // 'newDigest' is an optional parameter, provided in some code paths.

            if (_digestsToAnnotate.TryAdd(digest, eolDate))
            {
                // We do not check for dangling digests if annotating new digest/image/repo.
                if (!isNewDigest)
                {
                    AnnotateDanglingDigests(digest, tag, newDigest: newDigest);
                }
            }
            else if (eolDate != null)
            {
                _digestsToAnnotate[digest] = eolDate;
            }
        }

        private void AddImageForAnnotation(ImageData image, DateOnly? eolDate = null, bool isNewImage = false)
        {
            string tag = string.Empty;

            if (image.Manifest != null)
            {
                tag = image.Manifest.SharedTags.First();
                AddDigestForAnnotation(image.Manifest.Digest, tag, eolDate, isNewDigest: isNewImage);
            }

            foreach (PlatformData platform in image.Platforms)
            {
                if (platform.SimpleTags?.Count > 0)
                {
                    tag = platform.SimpleTags.First();
                }

                AddDigestForAnnotation(platform.Digest, tag, eolDate, isNewDigest: isNewImage);
            }
        }

        private void AddRepoForAnnotation(RepoData repo)
        {
            foreach (ImageData image in repo.Images)
            {
                AddImageForAnnotation(image);
            }
        }

        private void AnnotateImageIfProductIsEol(ImageData image)
        {
            if (image.ProductVersion != null)
            {
                string dotnetVersion = DotnetVersion(image.ProductVersion);
                if (_productEolDates != null && _productEolDates.TryGetValue(dotnetVersion, out DateOnly? date))
                {
                    AddImageForAnnotation(image, eolDate: date, isNewImage: true);
                }
            }
        }

        private static string DotnetVersion(string productVersion)
        {
            string[] versionParts = productVersion.Split('.');
            return versionParts.Length >= 2 ? versionParts[0] + "." + versionParts[1] : productVersion;
        }

        private static string ImageIdentityString(ImageData image) =>
            image.ProductVersion + (image.Manifest?.SharedTags != null ? " " + string.Join(" ", image.Manifest.SharedTags.Order()) : "");

        private static ImageArtifactDetails LoadImageInfoData(string imageInfoPath)
        {
            string imageInfoJson = File.ReadAllText(imageInfoPath);
            ImageArtifactDetails? imageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(imageInfoJson);
            return imageArtifactDetails is null
                ? throw new JsonException($"Unable to correctly deserialize path '{imageInfoJson}'.")
                : imageArtifactDetails;
        }
    }
}
