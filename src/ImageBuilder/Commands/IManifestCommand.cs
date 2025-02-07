﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.DockerTools.ImageBuilder.ViewModel;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Commands
{
    public interface IManifestCommand : ICommand
    {
        ManifestInfo Manifest { get; }

        void LoadManifest();
    }
}
