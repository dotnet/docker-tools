// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Http.Headers;
using System.Text;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

/// <summary>
/// Provides the credentials and identity used to access an Azure DevOps repository.
/// </summary>
public sealed record AzureDevOpsAccess
{
    /// <summary>
    /// Creates Azure DevOps repository access.
    /// </summary>
    /// <param name="authorization">The HTTP authorization header used for REST and git operations.</param>
    /// <param name="identity">The git identity represented by the credentials.</param>
    /// <param name="client">The HTTP client used for Azure DevOps REST requests.</param>
    public AzureDevOpsAccess(AuthenticationHeaderValue authorization, AutomationIdentity identity, HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(client);

        Authorization = authorization;
        Identity = identity;
        Client = client;
    }

    /// <summary>The HTTP authorization header used for REST and git operations.</summary>
    public AuthenticationHeaderValue Authorization { get; }

    /// <summary>The git identity represented by the credentials.</summary>
    public AutomationIdentity Identity { get; }

    /// <summary>The HTTP client used for Azure DevOps REST requests.</summary>
    public HttpClient Client { get; }
}

/// <summary>
/// Provides access to Azure DevOps repositories.
/// </summary>
public interface IAzureDevOpsAccessProvider
{
    /// <summary>
    /// Gets credentials and identity that are valid for the specified repository.
    /// </summary>
    /// <param name="repository">The repository to access.</param>
    /// <param name="cancellationToken">A token that cancels access acquisition.</param>
    /// <returns>Credentials and identity for the repository.</returns>
    ValueTask<AzureDevOpsAccess> GetAccessAsync(AzureDevOpsRepo repository, CancellationToken cancellationToken);
}

/// <summary>
/// The authentication scheme used by a fixed Azure DevOps token.
/// </summary>
public enum AzureDevOpsAuthenticationType
{
    /// <summary>An OAuth or Microsoft Entra bearer token.</summary>
    Bearer,

    /// <summary>An Azure DevOps personal access token.</summary>
    PersonalAccessToken,
}

/// <summary>
/// Provides fixed credentials for Azure DevOps repositories.
/// </summary>
public sealed class StaticAzureDevOpsAccessProvider : IAzureDevOpsAccessProvider
{
    private static readonly HttpClient s_httpClient = new();
    private readonly AzureDevOpsAccess _access;

    /// <summary>
    /// Creates a fixed Azure DevOps access provider.
    /// </summary>
    /// <param name="token">The OAuth, Microsoft Entra, or personal access token.</param>
    /// <param name="identity">The git identity represented by the token.</param>
    /// <param name="authenticationType">The token's authentication scheme.</param>
    /// <param name="client">
    /// The HTTP client used for REST requests. Omit to use a shared default client.
    /// </param>
    public StaticAzureDevOpsAccessProvider(
        string token,
        AutomationIdentity identity,
        AzureDevOpsAuthenticationType authenticationType = AzureDevOpsAuthenticationType.Bearer,
        HttpClient? client = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(identity);

        AuthenticationHeaderValue authorization = authenticationType switch
        {
            AzureDevOpsAuthenticationType.Bearer => new("Bearer", token),
            AzureDevOpsAuthenticationType.PersonalAccessToken => new(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($":{token}"))),
            _ => throw new ArgumentOutOfRangeException(nameof(authenticationType)),
        };

        _access = new(authorization, identity, client ?? s_httpClient);
    }

    /// <inheritdoc/>
    public ValueTask<AzureDevOpsAccess> GetAccessAsync(AzureDevOpsRepo repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_access);
    }
}
