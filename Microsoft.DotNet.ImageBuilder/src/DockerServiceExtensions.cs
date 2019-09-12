// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class DockerServiceExtensions
    {
        public static void PullBaseImages(this IDockerService dockerService, ManifestInfo manifest, ManifestOptions options)
        {
            Logger.WriteHeading("PULLING LATEST BASE IMAGES");
            IEnumerable<string> baseImages = manifest.GetExternalFromImages().ToArray();
            if (baseImages.Any())
            {
                foreach (string fromImage in baseImages)
                {
                    dockerService.PullImage(fromImage, options.IsDryRun);
                }
            }
            else
            {
                Logger.WriteMessage("No external base images to pull");
            }
        }
    }
}
