// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using Microsoft.DotNet.ImageBuilder.Configuration;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class ServiceConnectionOptionsBuilder
{
    public IEnumerable<Option> GetCliOptions(string alias, string propertyName, string description = "")
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

        var option = CreateOption(
            alias,
            propertyName,
            description,
            parseArg: result =>
            {
                var token = result.Tokens.Single();
                var serviceConnectionInfo = token.Value.Split(':');

                return new ServiceConnection()
                {
                    TenantId = serviceConnectionInfo[0],
                    ClientId = serviceConnectionInfo[1],
                    Id = serviceConnectionInfo[2],
                };
            });

        return [option];
    }
}
