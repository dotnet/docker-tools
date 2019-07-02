// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Image;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class ImageInfoHelper
    {
        public static void MergeRepos(RepoData[] srcRepos, List<RepoData> targetRepos)
        {
            foreach (RepoData srcRepo in srcRepos)
            {
                RepoData targetRepo = targetRepos.FirstOrDefault(r => r.Repo == srcRepo.Repo);
                if (targetRepo == null)
                {
                    targetRepos.Add(srcRepo);
                }
                else
                {
                    MergeImages(srcRepo, targetRepo);
                }
            }
        }

        private static void MergeImages(RepoData srcRepo, RepoData targetRepo)
        {
            if (srcRepo.Images == null)
            {
                return;
            }

            if (srcRepo.Images.Any() && targetRepo.Images == null)
            {
                targetRepo.Images = srcRepo.Images;
                return;
            }

            foreach (KeyValuePair<string, ImageData> srcKvp in srcRepo.Images)
            {
                if (targetRepo.Images.TryGetValue(srcKvp.Key, out ImageData targetImage))
                {
                    MergeDigests(srcKvp.Value, targetImage);
                }
                else
                {
                    targetRepo.Images.Add(srcKvp.Key, srcKvp.Value);
                }
            }
        }

        private static void MergeDigests(ImageData srcImage, ImageData targetImage)
        {
            if (srcImage.BaseImages == null)
            {
                return;
            }

            if (srcImage.BaseImages.Any() && targetImage.BaseImages == null)
            {
                targetImage.BaseImages = srcImage.BaseImages;
                return;
            }

            foreach (KeyValuePair<string, string> srcKvp in srcImage.BaseImages)
            {
                targetImage.BaseImages[srcKvp.Key] = srcKvp.Value;
            }
        }
    }
}
