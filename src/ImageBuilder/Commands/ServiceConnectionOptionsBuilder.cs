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
    public Option<ServiceConnection?> GetCliOption(string optionName, string description = "")
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

        return new Option<ServiceConnection?>(optionName)
        {
            Description = description,
            CustomParser = result =>
            {
                string token = result.Tokens.Single().Value;
                string[] parts = token.Split(':');

                if (parts.Length != 3)
                {
                    result.AddError(
                        $"Invalid service connection format '{token}'. " +
                        $"Expected format: \"{{tenantId}}:{{clientId}}:{{serviceConnectionId}}\".");
                    return null;
                }

                return new ServiceConnection()
                {
                    TenantId = parts[0],
                    ClientId = parts[1],
                    Id = parts[2],
                };
            }
        };
    }
}
