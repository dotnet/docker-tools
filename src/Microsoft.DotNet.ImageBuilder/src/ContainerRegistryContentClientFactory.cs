// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Azure.Containers.ContainerRegistry;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
[Export(typeof(IContainerRegistryContentClientFactory))]
internal class ContainerRegistryContentClientFactory : IContainerRegistryContentClientFactory
{
    private readonly CachedTokenCredential _credential;

    public ContainerRegistryContentClientFactory()
    {
        AccessToken token = new DefaultAzureCredential().GetToken(new TokenRequestContext(["https://containerregistry.azure.net/.default"]), CancellationToken.None);
        _credential = new CachedTokenCredential(token);
    }

    public IContainerRegistryContentClient Create(string acrName, string repositoryName, TokenCredential credential)
    {
        return new ContainerRegistryContentClientWrapper(new ContainerRegistryContentClient(DockerHelper.GetAcrUri(acrName), repositoryName, _credential));
    }

    private class CachedTokenCredential(AccessToken accessToken) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => accessToken;
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(accessToken);
    }
}
