// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

namespace Microsoft.DotNet.ImageBuilder.Commands;

public record ServiceConnectionOptions(
    string TenantId,
    string ClientId,
    string ServiceConnectionId)
    : IServiceConnection;

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

                return new ServiceConnectionOptions(
                    TenantId: serviceConnectionInfo[0],
                    ClientId: serviceConnectionInfo[1],
                    ServiceConnectionId: serviceConnectionInfo[2]);
            });

        return [option];
    }
}
