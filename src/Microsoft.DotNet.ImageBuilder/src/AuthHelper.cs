// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class AuthHelper
    {
        public static async Task<string> GetAadAccessTokenAsync(string resource, string tenant, string username, string password)
        {
            AuthenticationContext authContext = new AuthenticationContext($"https://login.microsoftonline.com/{tenant}");
            AuthenticationResult result = await authContext.AcquireTokenAsync(
                resource, new ClientCredential(username, password));
            return result.AccessToken;
        }
    }
}
