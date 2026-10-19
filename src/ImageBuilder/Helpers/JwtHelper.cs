// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.DotNet.ImageBuilder.Helpers;

public static class JwtHelper
{
    /// <summary>
    /// Creates a JWT token using the provided issuer and base64 encoded private key (PEM format).
    /// </summary>
    public static string CreateJwt(string issuer, string base64PrivateKey, TimeSpan timeout)
    {
        string privateKey = DecodeBase64String(base64PrivateKey);

        RsaSecurityKey rsaSecurityKey;
        using (RSA rsa = RSA.Create(4096))
        {
            rsa.ImportFromPem(privateKey);
            rsaSecurityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        }

        DateTime now = DateTime.UtcNow;
        SigningCredentials signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = now,
            Expires = now + timeout,
            Issuer = issuer,
            SigningCredentials = signingCredentials
        };

        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        JwtSecurityToken jwt = tokenHandler.CreateJwtSecurityToken(descriptor);
        return tokenHandler.WriteToken(jwt);
    }

    private static string DecodeBase64String(string input)
    {
        byte[] data = Convert.FromBase64String(input);
        return System.Text.Encoding.UTF8.GetString(data);
    }
}
