// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class AuthHelper
    {
        private const string DefaultScope = "https://management.azure.com/.default";

        public static async Task<string> GetDefaultAccessTokenAsync(string resource = DefaultScope)
        {
            DefaultAzureCredential credential  = new();
            AccessToken token = await credential.GetTokenAsync(new TokenRequestContext([ resource ]));
            return token.Token;
        }
    }
}
