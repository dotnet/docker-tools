// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel.Composition;
using System.Reflection;
using Microsoft.DotNet.ImageBuilder.Commands;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IOctokitClientFactory))]
    public class OctokitClientFactory : IOctokitClientFactory
    {
        private static readonly ProductHeaderValue s_productHeaderValue =
            new(name: Assembly.GetExecutingAssembly().GetName().Name);

        public IGitHubClient CreateGitHubClient(GitHubAuthOptions authOptions)
        {
            var client = new GitHubClient(s_productHeaderValue)
            {
                Credentials = GetCredentials(authOptions)
            };

            return client;
        }

        public IBlobsClient CreateBlobsClient(GitHubAuthOptions authOptions) =>
            new BlobsClient(CreateApiConnection(authOptions));

        public ITreesClient CreateTreesClient(GitHubAuthOptions authOptions) =>
            new TreesClient(CreateApiConnection(authOptions));

        private static ApiConnection CreateApiConnection(GitHubAuthOptions authOptions)
        {
            var connection = new Connection(s_productHeaderValue)
            {
                Credentials = GetCredentials(authOptions),
            };

            return new ApiConnection(connection);
        }

        private static Credentials GetCredentials(GitHubAuthOptions authOptions)
        {
            if (authOptions.IsGitHubAppAuth)
            {
                throw new NotImplementedException("""
                    Private key authentication is not implemented.
                    Please see https://github.com/dotnet/docker-tools/issues/1656.
                    """);
            }

            return new Credentials(authOptions.AuthToken);
        }
    }
}
