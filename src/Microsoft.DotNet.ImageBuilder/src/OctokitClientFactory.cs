// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel.Composition;
using System.Reflection;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IOctokitClientFactory))]
    public class OctokitClientFactory : IOctokitClientFactory
    {
        public static IApiConnection CreateApiConnection(Credentials credentials)
        {
            Connection connection = new(new ProductHeaderValue(Assembly.GetExecutingAssembly().GetName().Name));
            connection.Credentials = credentials;
            return new ApiConnection(connection);
        }

        public IBlobsClient CreateBlobsClient(IApiConnection connection) =>
            new BlobsClient(connection);

        public ITreesClient CreateTreesClient(IApiConnection connection) =>
            new TreesClient(connection);

    }
}
#nullable disable
