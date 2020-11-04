// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Valleysoft.DockerfileModel;
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
        public string ReadmePath { get; private set; }
        public string ReadmeTemplatePath { get; private set; }

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
                FullModelName = ImageName.Parse(
                    (string.IsNullOrEmpty(modelRegistryName) ? string.Empty : $"{modelRegistryName}/") + model.Name).ToString(),
                Id = model.Id ?? model.Name
            };

            repoInfo.QualifiedName = new ImageName(options.RepoPrefix + model.Name, registry).ToString();

            if (model.Readme != null)
            {
                repoInfo.ReadmePath = Path.Combine(baseDirectory, model.Readme);
            }
            if (model.ReadmeTemplate != null)
            {
                repoInfo.ReadmeTemplatePath = Path.Combine(baseDirectory, model.ReadmeTemplate);
            }

            repoInfo.AllImages = model.Images
                .Select(image => ImageInfo.Create(image, repoInfo.FullModelName, repoInfo.QualifiedName, manifestFilter, variableHelper, baseDirectory))
                .ToArray();

            repoInfo.FilteredImages = repoInfo.AllImages
                .Where(image => image.FilteredPlatforms.Any())
                .ToArray();

            return repoInfo;
        }

        public string GetReadmeContent()
        {
            if (ReadmePath == null)
            {
                throw new InvalidOperationException("A readme path was not specified in the manifest");
            }

            return File.ReadAllText(ReadmePath);
        }
    }
}
