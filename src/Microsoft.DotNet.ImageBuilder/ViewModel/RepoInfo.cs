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
        public IEnumerable<ImageInfo> Images { get; private set; }
        public Repo Model { get; private set; }

        private RepoInfo()
        {
        }

        public static RepoInfo Create(Repo model, Manifest manifest, Options options, string dockerOS)
        {
            RepoInfo repoInfo = new RepoInfo();
            repoInfo.Model = model;
            repoInfo.Images = model.Images
                .Select(image => ImageInfo.Create(image, manifest, model.Name, options, dockerOS))
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

        public bool IsExternalImage(string image)
        {
            return !image.StartsWith($"{Model.Name}:");
        }
    }
}
