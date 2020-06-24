// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ServicePrincipalOptions
    {
        public string ClientId { get; set; }

        public string Secret { get; set; }

        public string Tenant { get; set; }

        public void DefineParameters(ArgumentSyntax syntax)
        {
            string username = null;
            syntax.DefineParameter("client-id", ref username, "Client ID of service principal");
            ClientId = username;

            string password = null;
            syntax.DefineParameter("secret", ref password, "Secret of service principal");
            Secret = password;

            string tenant = null;
            syntax.DefineParameter("tenant", ref tenant, "Tenant of service principal");
            Tenant = tenant;
        }
    }
}
