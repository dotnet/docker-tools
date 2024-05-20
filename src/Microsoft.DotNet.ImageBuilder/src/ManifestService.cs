// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder;

// Wrapper for ManifestService extension methods required for unit testing 
public class ManifestService(IInnerManifestService inner) : IManifestService
{
    private readonly IInnerManifestService _inner = inner;

    public Task<string?> GetImageDigestAsync(string image, bool isDryRun) =>
        _inner.GetImageDigestAsync(image, isDryRun);

    public Task<IEnumerable<string>> GetImageLayersAsync(string tag, bool isDryRun) =>
        _inner.GetImageLayersAsync(tag, isDryRun);

    public Task<ManifestQueryResult> GetManifestAsync(string image, bool isDryRun) =>
        _inner.GetManifestAsync(image, isDryRun);

    public Task<string> GetManifestDigestShaAsync(string tag, bool isDryRun) =>
        _inner.GetManifestDigestShaAsync(tag, isDryRun);
}
