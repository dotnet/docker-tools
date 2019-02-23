// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Model;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class RepoInfo
    {
        /// <summary>
        /// All of the images that are defined in the manifest for this repo.
        /// </summary>
        public IEnumerable<ImageInfo> AllImages { get; private set; }

        /// <summary>
        /// The subet of image platforms after applying the command line filter options.
        /// </summary>
        public IEnumerable<ImageInfo> FilteredImages { get; private set; }

        public string FullModelName { get; private set; }    
        public string Id { get; private set; }
        public string Name { get; private set; }
        public Repo Model { get; private set; }

        private RepoInfo()
        {
        }

        public static RepoInfo Create(
            Repo model,
            string registry,
            string modelRegistryName,
            ManifestFilter manifestFilter,
            IOptionsInfo options,
            VariableHelper variableHelper)
        {
            RepoInfo repoInfo = new RepoInfo();
            repoInfo.Model = model;
            repoInfo.FullModelName = (string.IsNullOrEmpty(modelRegistryName) ? string.Empty : $"{modelRegistryName}/") + model.Name;
            repoInfo.Id = model.Id ?? model.Name;

            if (options.RepoOverrides.TryGetValue(model.Name, out string nameOverride))
            {
                repoInfo.Name = nameOverride;
            }
            else
            {
                registry = string.IsNullOrEmpty(registry) ? string.Empty : $"{registry}/";
                repoInfo.Name = registry + options.RepoPrefix + model.Name;
            }

            repoInfo.AllImages = model.Images
                .Select(image => ImageInfo.Create(image, repoInfo.FullModelName, repoInfo.Name, manifestFilter, variableHelper))
                .ToArray();

            repoInfo.FilteredImages = repoInfo.AllImages
                .Where(image => image.FilteredPlatforms.Any())
                .ToArray();

            return repoInfo;
        }

        public string GetReadmeContent()
        {
            if (Model.ReadmePath == null)
            {
                throw new InvalidOperationException("A readme path was not specified in the manifest");
            }

            return File.ReadAllText(Model.ReadmePath);
        }
    }
}
