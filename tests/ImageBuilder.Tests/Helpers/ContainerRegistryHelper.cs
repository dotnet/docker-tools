// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Azure;
using Azure.Containers.ContainerRegistry;
using Azure.Core;
using Moq;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Tests.Helpers;

#nullable enable
internal static class ContainerRegistryHelper
{
    public static ContainerRepository CreateContainerRepository(
            string repoName, ContainerRepositoryProperties? repositoryProperties = null, params ArtifactManifestProperties[] manifestProperties)
    {
        Mock<ContainerRepository> repoMock = new();
        repoMock.SetupGet(o => o.Name).Returns(repoName);
        if (repositoryProperties is not null)
        {
            repoMock
                .Setup(o => o.GetProperties(It.IsAny<CancellationToken>()))
                .Returns(CreateAzureResponse(repositoryProperties));
        }
        repoMock
            .Setup(o => o.GetAllManifestPropertiesAsync(It.IsAny<ArtifactManifestOrder>(), It.IsAny<CancellationToken>()))
            .Returns(new AsyncPageableMock<ArtifactManifestProperties>(manifestProperties));
        return repoMock.Object;
    }

    public static Mock<IContainerRegistryContentClient> CreateContainerRegistryContentClientMock(string repositoryName, Dictionary<string, ManifestQueryResult>? imageNameToQueryResultsMapping = null)
    {
        Mock<IContainerRegistryContentClient> acrClientContentMock = new();
        acrClientContentMock.SetupGet(o => o.RepositoryName).Returns(repositoryName);
        if (imageNameToQueryResultsMapping is not null)
        {
            foreach (KeyValuePair<string, ManifestQueryResult> kvp in imageNameToQueryResultsMapping)
            {
                acrClientContentMock.Setup(o => o.GetManifestAsync(kvp.Key)).ReturnsAsync(kvp.Value);
            }
        }

        return acrClientContentMock;
    }

    public static IContainerRegistryContentClientFactory CreateContainerRegistryContentClientFactory(
        string acrName, IEnumerable<Mock<IContainerRegistryContentClient>> acrContentClients)
    {
        Mock<IContainerRegistryContentClientFactory> acrContentClientFactoryMock = new();
        foreach (Mock<IContainerRegistryContentClient> clientMock in acrContentClients)
        {
            acrContentClientFactoryMock
                .Setup(o => o.Create(acrName, clientMock.Object.RepositoryName, It.IsAny<TokenCredential>()))
                .Returns(clientMock.Object);
        }

        return acrContentClientFactoryMock.Object;
    }

    public static Response<T> CreateAzureResponse<T>(T obj)
            where T : class
    {
        Mock<Response<T>> response = new();
        response.SetupGet(o => o.Value).Returns(obj);
        return response.Object;
    }

    public static ContainerRepositoryProperties CreateContainerRepositoryProperties(
        string registryLoginServer = "",
        string name = "",
        DateTimeOffset createdOn = default,
        DateTimeOffset lastUpdatedOn = default,
        int manifestCount = 0,
        int tagCount = 0)
    {
        // Have to use reflection here because the state we need to set is not settable from a public interface.
        // This is configured to invoke this constructor:
        // https://github.com/Azure/azure-sdk-for-net/blob/3d9f007d34562731419932dd987074662a3c2c1f/sdk/containerregistry/Azure.Containers.ContainerRegistry/src/Generated/Models/ContainerRepositoryProperties.cs#L22
        // IMPORTANT: The signature of this method must match the signature of the constructor being invoked.

        object[] args =
            [
                registryLoginServer,
                    name,
                    createdOn,
                    lastUpdatedOn,
                    manifestCount,
                    tagCount
            ];

        MethodInfo thisMethod = typeof(ContainerRegistryHelper).GetMethod(nameof(CreateContainerRepositoryProperties), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Unable to find method");
        ConstructorInfo ctor = typeof(ContainerRepositoryProperties).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            thisMethod.GetParameters().Select(param => param.ParameterType).ToArray()) ?? throw new Exception("Unable to find constructor");
        return (ContainerRepositoryProperties)ctor.Invoke(args);
    }

    public static Mock<IContainerRegistryClient> CreateContainerRegistryClientMock(IEnumerable<ContainerRepository> repositories)
    {
        Mock<IContainerRegistryClient> acrClientMock = new();

        IAsyncEnumerable<string> repositoryNames = repositories
            .Select(repo => repo.Name)
            .ToAsyncEnumerable();

        acrClientMock
            .Setup(o => o.GetRepositoryNamesAsync())
            .Returns(repositoryNames);

        foreach (ContainerRepository repo in repositories)
        {
            acrClientMock
                .Setup(o => o.GetRepository(repo.Name))
                .Returns(repo);
        }

        return acrClientMock;
    }

    public static IContainerRegistryClientFactory CreateContainerRegistryClientFactory(string acrName, IContainerRegistryClient acrClient)
    {
        Mock<IContainerRegistryClientFactory> acrClientFactoryMock = new();
        acrClientFactoryMock
            .Setup(o => o.Create(acrName, It.IsAny<TokenCredential>()))
            .Returns(acrClient);
        return acrClientFactoryMock.Object;
    }

    public static ArtifactManifestProperties CreateArtifactManifestProperties(
        string registryLoginServer = "",
        string repositoryName = "",
        string digest = "",
        long? sizeInBytes = null,
        DateTimeOffset createdOn = default,
        DateTimeOffset lastUpdatedOn = default,
        ArtifactArchitecture? architecture = null,
        ArtifactOperatingSystem? operatingSystem = null,
        IReadOnlyList<ArtifactManifestPlatform>? relatedArtifacts = null,
        IReadOnlyList<string>? tags = null,
        bool? canDelete = null,
        bool? canWrite = null,
        bool? canList = null,
        bool? canRead = null)
    {
        // Have to use reflection here because the state we need to set is not settable from a public interface.
        // This is configured to invoke this constructor:
        // https://github.com/Azure/azure-sdk-for-net/blob/3d9f007d34562731419932dd987074662a3c2c1f/sdk/containerregistry/Azure.Containers.ContainerRegistry/src/Generated/Models/ArtifactManifestProperties.cs#L44
        // IMPORTANT: The signature of this method must match the signature of the constructor being invoked.

        tags ??= [];

        object?[] args =
            [
                registryLoginServer,
                repositoryName,
                digest,
                sizeInBytes,
                createdOn,
                lastUpdatedOn,
                architecture,
                operatingSystem,
                relatedArtifacts,
                tags,
                canDelete,
                canWrite,
                canList,
                canRead
            ];

        MethodInfo thisMethod = typeof(ContainerRegistryHelper).GetMethod(nameof(CreateArtifactManifestProperties), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Unable to find method");

        ConstructorInfo ctor = typeof(ArtifactManifestProperties).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            thisMethod.GetParameters().Select(param => param.ParameterType).ToArray()) ?? throw new Exception("Unable to find constructor");
        return (ArtifactManifestProperties)ctor.Invoke(args);
    }
}
