// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Models.Image;
using Microsoft.DotNet.ImageBuilder.ViewModel;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageInfoHelper
    {
        public static ImageArtifactDetails LoadFromContent(string imageInfoContent, ManifestInfo manifest, bool skipManifestValidation = false)
        {
            ImageArtifactDetails imageArtifactDetails = JsonConvert.DeserializeObject<ImageArtifactDetails>(imageInfoContent);

            foreach (RepoData repoData in imageArtifactDetails.Repos)
            {
                RepoInfo manifestRepo = manifest.AllRepos.FirstOrDefault(repo => repo.Name == repoData.Repo);
                if (manifestRepo == null)
                {
                    Console.WriteLine($"Image info repo not loaded: {repoData.Repo}");
                    continue;
                }
                foreach (ImageData imageData in repoData.Images)
                {
                    imageData.ManifestRepo = manifestRepo;

                    PlatformData platformData = imageData.Platforms.FirstOrDefault();
                    if (platformData != null)
                    {
                        foreach (ImageInfo manifestImage in manifestRepo.AllImages)
                        {
                            PlatformInfo matchingManifestPlatform = manifestImage.AllPlatforms
                                .FirstOrDefault(platform => platformData.Equals(platform));
                            if (matchingManifestPlatform != null)
                            {
                                imageData.ManifestImage = manifestImage;
                                platformData.PlatformInfo = matchingManifestPlatform;
                                platformData.ImageInfo = manifestImage;
                                break;
                            }
                        }

                        if (!skipManifestValidation && imageData.ManifestImage == null)
                        {
                            throw new InvalidOperationException(
                                $"Unable to find matching platform in manifest for platform '{platformData.GetIdentifier()}'.");
                        }
                    }
                }
            }

            return imageArtifactDetails;
        }

        public static ImageArtifactDetails LoadFromFile(string path, ManifestInfo manifest, bool skipManifestValidation = false)
        {
            return LoadFromContent(File.ReadAllText(path), manifest, skipManifestValidation);
        }

        public static void MergeImageArtifactDetails(ImageArtifactDetails src, ImageArtifactDetails target, ImageInfoMergeOptions options = null)
        {
            if (options == null)
            {
                options = new ImageInfoMergeOptions();
            }

            MergeData(src, target, options);
        }

        private static void MergeData(object srcObj, object targetObj, ImageInfoMergeOptions options)
        {
            if (!((srcObj is null && targetObj is null) || (!(srcObj is null) && !(targetObj is null))))
            {
                throw new InvalidOperationException("The src and target objects must either be both null or both non-null.");
            }

            if (srcObj is null)
            {
                return;
            }

            if (srcObj.GetType() != targetObj.GetType())
            {
                throw new ArgumentException("Object types don't match.", nameof(targetObj));
            }

            IEnumerable<PropertyInfo> properties = srcObj.GetType().GetProperties()
                .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() == null);

            foreach (PropertyInfo property in properties)
            {
                if (property.PropertyType == typeof(string) ||
                    property.PropertyType == typeof(DateTime) ||
                    property.PropertyType == typeof(bool))
                {
                    property.SetValue(targetObj, property.GetValue(srcObj));
                }
                else if (typeof(IDictionary).IsAssignableFrom(property.PropertyType))
                {
                    MergeDictionaries(property, srcObj, targetObj, options);
                }
                else if (typeof(IList<string>).IsAssignableFrom(property.PropertyType))
                {
                    if (options.ReplaceTags &&
                        ((srcObj is PlatformData && property.Name == nameof(PlatformData.SimpleTags)) ||
                        (srcObj is ManifestData && property.Name == nameof(ManifestData.SharedTags))))
                    {
                        // Tags can be merged or replaced depending on the scenario.
                        // When merging multiple image info files together into a single file, the tags should be
                        // merged to account for cases where tags for a given image are spread across multiple
                        // image info files.  But when publishing an image info file the source tags should replace
                        // the destination tags.  Any of the image's tags contained in the target should be considered
                        // obsolete and should be replaced by the source.  This accounts for the scenario where shared
                        // tags are moved from one image to another. If we had merged instead of replaced, then the
                        // shared tag would not have been removed from the original image in the image info in such
                        // a scenario.
                        // See:
                        // - https://github.com/dotnet/docker-tools/pull/269
                        // - https://github.com/dotnet/docker-tools/issues/289

                        ReplaceValue(property, srcObj, targetObj);
                    }
                    else
                    {
                        MergeStringLists(property, srcObj, targetObj);
                    }
                }
                else if (typeof(IList<ImageData>).IsAssignableFrom(property.PropertyType))
                {
                    MergeLists<ImageData>(property, srcObj, targetObj, options);
                }
                else if (typeof(IList<PlatformData>).IsAssignableFrom(property.PropertyType))
                {
                    MergeLists<PlatformData>(property, srcObj, targetObj, options);
                }
                else if (typeof(IList<RepoData>).IsAssignableFrom(property.PropertyType))
                {
                    MergeLists<RepoData>(property, srcObj, targetObj, options);
                }
                else if (typeof(ManifestData).IsAssignableFrom(property.PropertyType))
                {
                    MergeData(property.GetValue(srcObj), property.GetValue(targetObj), options);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported model property type: '{property.PropertyType.FullName}'");
                }
            }
        }

        private static void ReplaceValue(PropertyInfo property, object srcObj, object targetObj)
        {
            object value = property.GetValue(srcObj);
            if (value is IList<string> stringList)
            {
                value = stringList
                    .OrderBy(item => item)
                    .ToList<string>();
            }

            property.SetValue(targetObj, value);
        }

        private static void MergeStringLists(PropertyInfo property, object srcObj, object targetObj)
        {
            IList<string> srcList = (IList<string>)property.GetValue(srcObj);
            if (srcList == null)
            {
                return;
            }

            IList<string> targetList = (IList<string>)property.GetValue(targetObj);

            if (srcList.Any())
            {
                if (targetList != null)
                {
                    targetList = targetList
                        .Union(srcList)
                        .OrderBy(element => element)
                        .ToList();
                }
                else
                {
                    targetList = srcList;
                }

                property.SetValue(targetObj, targetList);
            }
        }

        private static void MergeLists<T>(PropertyInfo property, object srcObj, object targetObj, ImageInfoMergeOptions options)
            where T : IComparable<T>
        {
            IList<T> srcList = (IList<T>)property.GetValue(srcObj);
            if (srcList == null)
            {
                return;
            }

            IList<T> targetList = (IList<T>)property.GetValue(targetObj);

            if (srcList.Any())
            {
                if (targetList?.Any() == true)
                {
                    foreach (T srcItem in srcList)
                    {
                        T matchingTargetItem = targetList
                            .FirstOrDefault(targetItem => srcItem.CompareTo(targetItem) == 0);
                        if (matchingTargetItem != null)
                        {
                            MergeData(srcItem, matchingTargetItem, options);
                        }
                        else
                        {
                            targetList.Add(srcItem);
                        }
                    }
                }
                else
                {
                    targetList = srcList;
                }

                List<T> sortedList = targetList.ToList();
                sortedList.Sort();

                property.SetValue(targetObj, sortedList);
            }
        }

        private static void MergeDictionaries(PropertyInfo property, object srcObj, object targetObj,
            ImageInfoMergeOptions options)
        {
            IDictionary srcDict = (IDictionary)property.GetValue(srcObj);
            if (srcDict == null)
            {
                return;
            }

            IDictionary targetDict = (IDictionary)property.GetValue(targetObj);

            if (srcDict.Cast<object>().Any())
            {
                if (targetDict != null)
                {
                    foreach (dynamic kvp in srcDict)
                    {
                        if (targetDict.Contains(kvp.Key))
                        {
                            object newValue = kvp.Value;
                            if (newValue is string)
                            {
                                targetDict[kvp.Key] = newValue;
                            }
                            else
                            {
                                MergeData(kvp.Value, targetDict[kvp.Key], options);
                            }
                        }
                        else
                        {
                            targetDict[kvp.Key] = kvp.Value;
                        }
                    }
                }
                else
                {
                    property.SetValue(targetObj, srcDict);
                }
            }
        }
    }
}
