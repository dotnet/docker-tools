// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Models.Acr;

namespace Microsoft.DotNet.ImageBuilder
{
    public interface IAcrClient : IDisposable
    {
        Task<Catalog> GetCatalogAsync();

        Task<Repository> GetRepositoryAsync(string name);

        Task<DeleteRepositoryResponse> DeleteRepositoryAsync(string name);

        Task<RepositoryManifests> GetRepositoryManifestsAsync(string repositoryName);

        Task DeleteManifestAsync(string repositoryName, string digest);
    }
}
