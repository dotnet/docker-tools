// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.ImageBuilder.ViewModel
{
    public interface IManifestOptionsInfo : IOptionsInfo
    {
        string Manifest { get; }
        string RegistryOverride { get; }
        IDictionary<string, string> RepoOverrides { get; }
        string RepoPrefix { get; }
        IDictionary<string, string> Variables { get; }

        ManifestFilter GetManifestFilter();
    }
}
