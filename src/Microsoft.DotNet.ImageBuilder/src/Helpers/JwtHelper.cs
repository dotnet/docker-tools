// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.DotNet.ImageBuilder.Helpers;

public static class JwtHelper
{
    public static string CreateJwt(string issuer, string pemKeyFilePath, TimeSpan timeout)
    {
        string keyText = File.ReadAllText(pemKeyFilePath);

        RsaSecurityKey rsaSecurityKey;
        using (var rsa = RSA.Create(4096))
        {
            rsa.ImportFromPem(keyText);
            rsaSecurityKey = new RsaSecurityKey(rsa.ExportParameters(true));
        };

        var now = DateTime.UtcNow;
        var signingCredentials = new SigningCredentials(rsaSecurityKey, SecurityAlgorithms.RsaSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = now,
            Expires = now + timeout,
            Issuer = issuer,
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.CreateJwtSecurityToken(descriptor);
        return tokenHandler.WriteToken(jwt);
    }
}
