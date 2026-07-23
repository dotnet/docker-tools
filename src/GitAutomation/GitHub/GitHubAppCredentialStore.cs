// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Octokit;

namespace Microsoft.DotNet.GitAutomation.GitHub;

internal sealed class GitHubAppCredentialStore(string clientId, CryptographyClient cryptographyClient)
    : ICredentialStore
{
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private Credentials? _credentials;
    private DateTimeOffset _expiresAt;

    public async Task<Credentials> GetCredentials()
    {
        // See https://docs.github.com/apps/creating-github-apps/authenticating-with-a-github-app/generating-a-json-web-token-jwt-for-a-github-app

        if (_credentials is not null && _expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _credentials;
        }

        await _refreshLock.WaitAsync();
        try
        {
            if (_credentials is not null && _expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _credentials;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            // GitHub's maximum expiration time is 10 minutes, stay a little under that.
            _expiresAt = now.AddMinutes(9);

            var header = new JwtHeader
            {
                [JwtHeaderParameterNames.Alg] = SecurityAlgorithms.RsaSha256,
                [JwtHeaderParameterNames.Typ] = "JWT",
            };

            var payload = new JwtPayload(
                issuer: clientId,
                audience: null,
                claims: null,
                notBefore: null,
                expires: _expiresAt.UtcDateTime,
                // Backdate issue time so differences between this machine's clock and GitHub's
                // server clock cannot make the JWT appear to come from the future.
                issuedAt: now.AddMinutes(-1).UtcDateTime);

            string unsignedJwt = $"{header.Base64UrlEncode()}.{payload.Base64UrlEncode()}";
            SignatureAlgorithm algorithm = new(SecurityAlgorithms.RsaSha256);
            byte[] data = Encoding.UTF8.GetBytes(unsignedJwt);
            // Sign the JWT with the private key in Azure Key Vault.
            SignResult signature = await cryptographyClient.SignDataAsync(algorithm, data);

            _credentials = new Credentials(
                $"{unsignedJwt}.{Base64UrlEncoder.Encode(signature.Signature)}",
                AuthenticationType.Bearer);

            return _credentials;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
