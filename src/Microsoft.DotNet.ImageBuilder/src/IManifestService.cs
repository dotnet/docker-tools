// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

public interface IManifestService
{
    Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun);
    Task<IEnumerable<string>> GetImageLayersAsync(string tag, bool isDryRun);
    Task<string?> GetImageDigestAsync(string image, bool isDryRun);
    Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun);
}
