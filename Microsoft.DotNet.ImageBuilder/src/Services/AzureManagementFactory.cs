// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.Composition;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    [Export(typeof(IAzureManagementFactory))]
    internal class AzureManagementFactory : IAzureManagementFactory
    {
        public IAzure CreateAzureManager(AzureCredentials credentials, string subscriptionId)
        {
            return Azure.Management.Fluent.Azure
                .Configure()
                .Authenticate(credentials)
                .WithSubscription(subscriptionId);
        }
    }
}
