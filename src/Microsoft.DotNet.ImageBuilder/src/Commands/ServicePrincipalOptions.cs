// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.CommandLine;
using static Microsoft.DotNet.ImageBuilder.Commands.CliHelper;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder.Commands
{
    public class ServicePrincipalOptions
    {
        public string ClientId { get; set; } = string.Empty;

        public string Secret { get; set; } = string.Empty;

        public string Tenant { get; set; } = string.Empty;
    }

    public class ServicePrincipalOptionsBuilder
    {
        private readonly List<Option> _options = new();
        private readonly List<Argument> _arguments = new();

        private ServicePrincipalOptionsBuilder()
        {
        }

        public static ServicePrincipalOptionsBuilder Build() => new();

        public static ServicePrincipalOptionsBuilder BuildWithDefaults() =>
            Build()
                .WithClientId(isRequired: true)
                .WithSecret(isRequired: true)
                .WithTenant(isRequired: true);

        public ServicePrincipalOptionsBuilder WithClientId(
            string alias = "sp-client-id",
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Client ID of service principal") =>
            AddSymbol(alias, nameof(ServicePrincipalOptions.ClientId), isRequired, defaultValue, description);

        public ServicePrincipalOptionsBuilder WithSecret(
            string alias = "sp-secret",
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Secret of service principal") =>
            AddSymbol(alias, nameof(ServicePrincipalOptions.Secret), isRequired, defaultValue, description);

        public ServicePrincipalOptionsBuilder WithTenant(
            string alias = "sp-tenant",
            bool isRequired = false,
            string? defaultValue = null,
            string description = "Tenant of service principal") =>
            AddSymbol(alias, nameof(ServicePrincipalOptions.Tenant), isRequired, defaultValue, description);

        public IEnumerable<Option> GetCliOptions() => _options;

        public IEnumerable<Argument> GetCliArguments() => _arguments;

        private ServicePrincipalOptionsBuilder AddSymbol<T>(string alias, string propertyName, bool isRequired, T? defaultValue, string description)
        {
            if (isRequired)
            {
                _arguments.Add(new Argument<T>(propertyName, description));
            }
            else
            {
                _options.Add(CreateOption<T>(alias, propertyName, description, defaultValue is null ? default! : defaultValue));
            }

            return this;
        }
    }
}
#nullable disable
