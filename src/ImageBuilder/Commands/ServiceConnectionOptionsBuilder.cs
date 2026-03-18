// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class ServiceConnectionOptionsBuilder
{
    /// <summary>
    /// Creates a single CLI option that parses a service connection string.
    /// </summary>
    public Option<ServiceConnection?> GetCliOption(string alias, string description = "")
    {
        const string FormatDescription = "Format: \"{tenantId}:{clientId}:{serviceConnectionId}\".";

        if (!string.IsNullOrEmpty(description))
        {
            description += " " + FormatDescription;
        }
        else
        {
            description = FormatDescription;
        }

        return new Option<ServiceConnection?>(CliHelper.FormatAlias(alias))
        {
            Description = description,
            CustomParser = result =>
            {
                string token = result.Tokens.Single().Value;
                string[] serviceConnectionInfo = token.Split(':');

                return new ServiceConnection()
                {
                    TenantId = serviceConnectionInfo[0],
                    ClientId = serviceConnectionInfo[1],
                    Id = serviceConnectionInfo[2],
                };
            }
        };
    }
}
