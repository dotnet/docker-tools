// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.Azure.Management.ContainerRegistry.Fluent;
using Microsoft.Azure.Management.ContainerRegistry.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.DotNet.ImageBuilder.Services;
using Microsoft.Rest.Azure;
using Moq;

namespace Microsoft.DotNet.ImageBuilder.Tests.Helpers
{
    public static class AzureHelper
    {
        public static Mock<IAzureManagementFactory> CreateAzureManagementFactoryMock(string subscriptionId, IAzure azure)
        {
            Mock<IAzureManagementFactory> azureManagementFactoryMock = new Mock<IAzureManagementFactory>();
            azureManagementFactoryMock
                .Setup(o => o.CreateAzureManager(It.IsAny<AzureCredentials>(), subscriptionId))
                .Returns(azure);
            return azureManagementFactoryMock;
        }

        public static IAzure CreateAzureMock(Mock<IRegistriesOperations> registriesOperationsMock) =>
            Mock.Of<IAzure>(o => o.ContainerRegistries.Inner == registriesOperationsMock.Object);

        public static Mock<IRegistriesOperations> CreateRegistriesOperationsMock()
        {
            Mock<IRegistriesOperations> registriesOperationsMock = new Mock<IRegistriesOperations>();
            registriesOperationsMock
                .Setup(o => o.ImportImageWithHttpMessagesAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ImportImageParametersInner>(),
                    It.IsAny<Dictionary<string, List<string>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AzureOperationResponse());
            return registriesOperationsMock;
        }
    }
}
