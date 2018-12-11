// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public class RepoInfo
    {
        public bool HasOverriddenName { get; set; }
        public string Name { get; private set; }
        public IEnumerable<ImageInfo> Images { get; private set; }
        public Repo Model { get; private set; }

        private RepoInfo()
        {
        }

        public static RepoInfo Create(Repo model, ManifestFilter manifestFilter, IOptionsInfo options, VariableHelper variableHelper)
        {
            RepoInfo repoInfo = new RepoInfo();
            repoInfo.Model = model;
            repoInfo.HasOverriddenName = options.RepoOverrides.TryGetValue(model.Name, out string nameOverride);
            repoInfo.Name = repoInfo.HasOverriddenName ? nameOverride : model.Name;
            repoInfo.Images = model.Images
                .Select(image => ImageInfo.Create(image, model, repoInfo.Name, manifestFilter, variableHelper))
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
