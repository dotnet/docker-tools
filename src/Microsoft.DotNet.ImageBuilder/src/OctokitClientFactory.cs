// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Helpers;
using Octokit;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IOctokitClientFactory))]
    [method: ImportingConstructor]
    public class OctokitClientFactory(ILoggerService loggerService) : IOctokitClientFactory
    {
        private static readonly ProductHeaderValue s_productHeaderValue =
            new(name: Assembly.GetExecutingAssembly().GetName().Name);

        private readonly ILoggerService _loggerService = loggerService;

        public async Task<IGitHubClient> CreateGitHubClientAsync(GitHubAuthOptions authOptions)
        {
            var credentials = await CreateCredentialsAsync(authOptions);
            var client = CreateClient(credentials);
            return client;
        }

        public async Task<IBlobsClient> CreateBlobsClientAsync(GitHubAuthOptions authOptions)
        {
            var apiConnection = await CreateApiConnectionAsync(authOptions);
            return new BlobsClient(apiConnection);
        }

        public async Task<ITreesClient> CreateTreesClientAsync(GitHubAuthOptions authOptions)
        {
            var apiConnection = await CreateApiConnectionAsync(authOptions);
            return new TreesClient(apiConnection);
        }

        /// <summary>
        /// Creates a GitHub token for the specified authentication options.
        /// </summary>
        /// <remarks>
        /// If authOptions specifies a GitHub App, a JWT token is created using
        /// the private key and client ID of the GitHub App.
        /// </remarks>
        /// <param name="authOptions"></param>
        /// <returns>
        /// A GitHub token created according to the authentication options.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no installations are found for the GitHub App specified by authOptions.
        /// </exception>
        public async Task<string> CreateGitHubTokenAsync(GitHubAuthOptions authOptions)
        {
            if (authOptions.IsGitHubAppAuth)
            {
                /**
                    Basic steps for GitHub App authentication:
                    1. Create a JWT token using the private key and client ID of the GitHub App.
                    2. Use the JWT token to authenticate as the GitHub App. This only allows you to query basic
                       information about the App itself.
                    3. Use the GitHub App client to get an token for a specific App installation.
                **/

                var jwt = CreateJwt(authOptions.ClientId, authOptions.PrivateKeyFilePath);
                var appCredentials = CreateCredentials(jwt);
                var appClient = CreateClient(appCredentials);
                var appInfo = await GetCurrentAppInfoAsync(appClient.Credentials);

                Installation? appInstallation = appInfo.Installations.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        $"""
                        No installations found for GitHub App.
                        Ensure the App is installed on the organization you are trying to access.

                        {appInfo}
                        """);

                var installationToken = await appClient.GitHubApps.CreateInstallationToken(appInstallation.Id);

                _loggerService.WriteMessage(
                    $"GitHub App token created for App ID {appInstallation.AppId} and installation "
                    + $"{appInstallation.Id} with expiration {installationToken.ExpiresAt}");

                return installationToken.Token;
            }

            return authOptions.AuthToken;
        }

        public IGitHubClient CreateGitHubClient(GitHubAuthOptions authOptions) =>
            CreateGitHubClientAsync(authOptions).GetAwaiter().GetResult();

        public ITreesClient CreateTreesClient(GitHubAuthOptions authOptions) =>
            CreateTreesClientAsync(authOptions).GetAwaiter().GetResult();

        public IBlobsClient CreateBlobsClient(GitHubAuthOptions authOptions) =>
            CreateBlobsClientAsync(authOptions).GetAwaiter().GetResult();

        public string CreateGitHubToken(GitHubAuthOptions authOptions) =>
            CreateGitHubTokenAsync(authOptions).GetAwaiter().GetResult();

        private async Task<ApiConnection> CreateApiConnectionAsync(GitHubAuthOptions authOptions)
        {
            var credentials = await CreateCredentialsAsync(authOptions);
            var connection = new Connection(s_productHeaderValue)
            {
                Credentials = credentials
            };

            return new ApiConnection(connection);
        }

        private async Task<Credentials> CreateCredentialsAsync(GitHubAuthOptions authOptions)
        {
            var token = await CreateGitHubTokenAsync(authOptions);
            return CreateCredentials(token);
        }

        /// <summary>
        /// Creates a JWT that can be used to authenticate as a GitHub App.
        /// </summary>
        /// <param name="clientId">The Client ID of the GitHub App. This is unique per App.</param>
        /// <param name="privateKeyFilePath">Path to the .pem file which contains the private key for the App.</param>
        /// <returns></returns>
        private static string CreateJwt(string clientId, string privateKeyFilePath)
        {
            // https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-json-web-token-jwt-for-a-github-app#about-json-web-tokens-jwts
            // > [The expiration time] must be no more than 10 minutes into the future.
            // Use 9 minutes to be safe.
            var timeout = TimeSpan.FromMinutes(9);
            return JwtHelper.CreateJwt(clientId, privateKeyFilePath, timeout);
        }

        /// <summary>
        /// Creates a GitHub client with common product header value
        /// </summary>
        /// <param name="credentials"></param>
        /// <returns></returns>
        private static GitHubClient CreateClient(Credentials credentials) =>
            new GitHubClient(s_productHeaderValue)
            {
                Credentials = credentials
            };

        /// <summary>
        /// Creates GitHub credentials with the correct authentication type
        /// </summary>
        /// <param name="token">The token to use for the credentials</param>
        /// <returns>The credentials</returns>
        private static Credentials CreateCredentials(string token) =>
            new Credentials(token, AuthenticationType.Bearer);

        private static async Task<GitHubAppInfoResult> GetCurrentAppInfoAsync(Credentials credentials)
        {
            var client = CreateClient(credentials);
            var currentApp = await client.GitHubApps.GetCurrent();
            var installations = await client.GitHubApps.GetAllInstallationsForCurrent();
            return new GitHubAppInfoResult(currentApp, installations);
        }

        private record GitHubAppInfoResult(
            GitHubApp CurrentApp,
            IReadOnlyList<Installation> Installations)
        {
            private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = true };

            public override string ToString()
            {
                string appInfoText = JsonSerializer.Serialize(CurrentApp, s_jsonOptions);
                string installationsText = JsonSerializer.Serialize(Installations, s_jsonOptions);

                return $"""

                App: {CurrentApp.Name}
                ---

                App Info: {appInfoText}

                App Installations: {installationsText}

                """;
            }
        };
    }
}
