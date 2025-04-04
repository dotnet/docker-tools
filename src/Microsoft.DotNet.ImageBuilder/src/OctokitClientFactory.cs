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
            new(Assembly.GetExecutingAssembly().GetName().Name);

        public IGitHubClient CreateGitHubClient(GitHubAuthOptions authOptions)
        {
            var client = new GitHubClient(s_productHeaderValue)
            {
                Credentials = GetCredentials(authOptions),
            };

            return client;
        }

        public IBlobsClient CreateBlobsClient(GitHubAuthOptions authOptions) =>
            new BlobsClient(GetApiConnection(authOptions));

        public ITreesClient CreateTreesClient(GitHubAuthOptions authOptions) =>
            new TreesClient(GetApiConnection(authOptions));

        private static ApiConnection GetApiConnection(GitHubAuthOptions authOptions)
        {
            Connection connection = new(s_productHeaderValue)
            {
                Credentials = GetCredentials(authOptions),
            };

            return new ApiConnection(connection);
        }

        private static Credentials GetCredentials(GitHubAuthOptions authOptions)
        {
            string gitHubAccessToken = authOptions.AuthToken
                ?? throw new InvalidOperationException("A GitHub access token is required to create API connection.");

            return new Credentials(gitHubAccessToken);
        }
    }
}
