// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.ViewModel;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public interface ICommand
    {
        ManifestInfo Manifest { get; }
        Options Options { get; }

        Task ExecuteAsync();

        void LoadManifest();
    }
}
