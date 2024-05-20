// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

namespace Microsoft.DotNet.ImageBuilder
{
    public static class AuthHelper
    {
        private const string DefaultScope = "https://management.azure.com/.default";

        public static async Task<(string token, Guid tenantId)> GetDefaultAccessTokenAsync(ILoggerService loggerService, string resource = DefaultScope)
        {
            DefaultAzureCredential credential  = new();
            AccessToken token = await credential.GetTokenAsync(new TokenRequestContext([ resource ]));
            Guid tenantId = GetTenantId(loggerService, credential);
            return (token.Token, tenantId);
        }

        public static Guid GetTenantId(ILoggerService loggerService, TokenCredential credential)
        {
            ArmClient armClient = new(credential);
            IEnumerable<Guid> tenants = armClient.GetTenants().ToList()
                .Select(tenantResource => tenantResource.Data.TenantId)
                .Where(guid => guid != null)
                .Select(guid => (Guid)guid);

            if (!tenants.Any())
            {
                throw new Exception("Found no tenants for given credential.");
            }

            if (tenants.Count() > 1)
            {
                string allTenantIds = string.Join(' ', tenants.Select(guid => guid.ToString()));
                loggerService.WriteMessage("Found more than one tenant. Selecting the first one.");
                loggerService.WriteMessage($"Tenants: {allTenantIds}");
            }

            return tenants.First();
        }
    }
}
