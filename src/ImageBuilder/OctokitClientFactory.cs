// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;
using Microsoft.DotNet.ImageBuilder.Helpers;
using Octokit;

namespace Microsoft.DotNet.ImageBuilder;

public class OctokitClientFactory(ILogger<OctokitClientFactory> logger) : IOctokitClientFactory
{
    private static readonly ProductHeaderValue ProductHeaderValue =
        new(name: Assembly.GetExecutingAssembly().GetName().Name);

    private readonly ILogger<OctokitClientFactory> _logger = logger;

    public async Task<IGitHubClient> CreateGitHubClientAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken)
    {
        Credentials credentials = await CreateCredentialsAsync(authOptions, cancellationToken);
        GitHubClient client = CreateClient(credentials);
        return client;
    }

    public async Task<IBlobsClient> CreateBlobsClientAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken)
    {
        ApiConnection apiConnection = await CreateApiConnectionAsync(authOptions, cancellationToken);
        return new BlobsClient(apiConnection);
    }

    public async Task<ITreesClient> CreateTreesClientAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken)
    {
        ApiConnection apiConnection = await CreateApiConnectionAsync(authOptions, cancellationToken);
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
    /// <param name="cancellationToken"></param>
    /// <returns>
    /// A GitHub token created according to the authentication options.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no installations are found for the GitHub App specified by authOptions.
    /// </exception>
    public async Task<string> CreateGitHubTokenAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken)
    {
        if (authOptions.IsGitHubAppAuth)
        {
            /*
                Basic steps for GitHub App authentication:
                1. Create a JWT token using the private key and client ID of the GitHub App.
                2. Use the JWT token to authenticate as the GitHub App. This only allows you to query basic
                   information about the App itself.
                3. Use the GitHub App client to get an token for a specific App installation.
            */
            string jwt = CreateJwt(authOptions.ClientId, authOptions.PrivateKey);
            Credentials appCredentials = CreateCredentials(jwt);
            GitHubClient appClient = CreateClient(appCredentials);
            GitHubAppInfoResult appInfo = await GetCurrentAppInfoAsync(appClient.Credentials);

            Installation appInstallation = authOptions.InstallationId is string providedId
                ? appInfo.Installations
                    .FirstOrDefault(installation => installation.Id.ToString() == providedId)
                        ?? throw new InvalidOperationException($"No installation found with ID {providedId}.")
                : appInfo.Installations.SingleOrDefault()
                    ?? throw new InvalidOperationException("Expected exactly one installation for GitHub App but "
                        + "found none or multiple. Provide an installation ID explicitly.");
            AccessToken installationToken = await appClient.GitHubApps.CreateInstallationToken(appInstallation.Id);

            _logger.LogInformation(
                $"GitHub App token created for App ID {appInstallation.AppId} and installation "
                + $"{appInstallation.Id} with expiration {installationToken.ExpiresAt}");

            return installationToken.Token;
        }

        return authOptions.AuthToken;
    }

    private async Task<ApiConnection> CreateApiConnectionAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken)
    {
        Credentials credentials = await CreateCredentialsAsync(authOptions, cancellationToken);
        Connection connection = new Connection(ProductHeaderValue)
        {
            Credentials = credentials
        };

        return new ApiConnection(connection);
    }

    private async Task<Credentials> CreateCredentialsAsync(GitHubAuthOptions authOptions, CancellationToken cancellationToken)
    {
        string token = await CreateGitHubTokenAsync(authOptions, cancellationToken);
        return CreateCredentials(token);
    }

    /// <summary>
    /// Creates a JWT that can be used to authenticate as a GitHub App.
    /// </summary>
    /// <param name="clientId">The Client ID of the GitHub App. This is unique per App.</param>
    /// <param name="privateKey">Base-64 encoded private key (PEM format) for the App.</param>
    /// <returns></returns>
    private static string CreateJwt(string clientId, string privateKey)
    {
        // https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-json-web-token-jwt-for-a-github-app#about-json-web-tokens-jwts
        // > [The expiration time] must be no more than 10 minutes into the future.
        // Use 9 minutes to be safe.
        TimeSpan timeout = TimeSpan.FromMinutes(9);
        return JwtHelper.CreateJwt(clientId, privateKey, timeout);
    }

    /// <summary>
    /// Creates a GitHub client with common product header value
    /// </summary>
    /// <param name="credentials"></param>
    /// <returns></returns>
    private static GitHubClient CreateClient(Credentials credentials) =>
        new GitHubClient(ProductHeaderValue)
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
        GitHubClient client = CreateClient(credentials);
        GitHubApp currentApp = await client.GitHubApps.GetCurrent();
        IReadOnlyList<Installation> installations = await client.GitHubApps.GetAllInstallationsForCurrent();
        return new GitHubAppInfoResult(currentApp, installations);
    }

    private sealed record GitHubAppInfoResult(
        GitHubApp CurrentApp,
        IReadOnlyList<Installation> Installations)
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public override string ToString()
        {
            string appInfoText = JsonSerializer.Serialize(CurrentApp, JsonOptions);
            string installationsText = JsonSerializer.Serialize(Installations, JsonOptions);

            return $"""

            App: {CurrentApp.Name}
            ---

            App Info: {appInfoText}

            App Installations: {installationsText}

            """;
        }
    };
}
