// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Models.Manifest;

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
        public string QualifiedName { get; private set; }
        public string Name => Model.Name;
        public Repo Model { get; private set; }
        public IEnumerable<Readme> Readmes { get; private set; }

        private RepoInfo()
        {
        }

        public static RepoInfo Create(
            Repo model,
            string registry,
            string modelRegistryName,
            ManifestFilter manifestFilter,
            IManifestOptionsInfo options,
            VariableHelper variableHelper,
            string baseDirectory)
        {
            RepoInfo repoInfo = new RepoInfo
            {
                Model = model,
                FullModelName = (string.IsNullOrEmpty(modelRegistryName) ? string.Empty : $"{modelRegistryName}/") + model.Name,
                Id = model.Id ?? model.Name
            };

            registry = string.IsNullOrEmpty(registry) ? string.Empty : $"{registry}/";
            repoInfo.QualifiedName = registry + options.RepoPrefix + model.Name;

            repoInfo.Readmes = model.Readmes
                ?.Select(readme => new Readme(Path.Combine(baseDirectory, readme.Path), Path.Combine(baseDirectory, readme.TemplatePath)))
                ?? Enumerable.Empty<Readme>();

            repoInfo.AllImages = model.Images
                .Select(image => ImageInfo.Create(image, repoInfo.FullModelName, repoInfo.QualifiedName, manifestFilter, variableHelper, baseDirectory))
                .ToArray();

            repoInfo.FilteredImages = repoInfo.AllImages
                .Where(image => image.FilteredPlatforms.Any())
                .ToArray();

            return repoInfo;
        }
    }
}
