// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Azure.Core;
using Azure.ResourceManager.ContainerRegistry.Models;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Abstracts ACR image import operations via the Azure Resource Manager SDK,
/// allowing tests to mock the ARM interaction.
/// </summary>
public interface IAcrImageImporter
{
    /// <summary>
    /// Imports an image into an Azure Container Registry using the ARM SDK.
    /// </summary>
    /// <param name="destAcrName">Destination ACR hostname used to resolve credentials.</param>
    /// <param name="destResourceId">ARM resource identifier for the destination registry.</param>
    /// <param name="importContent">Import parameters including source, tags, and mode.</param>
    Task ImportImageAsync(
        string destAcrName,
        ResourceIdentifier destResourceId,
        ContainerRegistryImportImageContent importContent);
}
