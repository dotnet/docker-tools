// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

/// <summary>Registers the Azure DevOps Git HTTP client.</summary>
public static class AzureDevOpsServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers an Azure DevOps client for one organization and project.</summary>
        /// <param name="organization">Azure DevOps organization name.</param>
        /// <param name="project">Azure DevOps project name or ID.</param>
        /// <param name="accessToken">Azure DevOps token. This can be System.AccessToken from Azure Pipelines.</param>
        /// <returns>The HTTP client builder.</returns>
        public IHttpClientBuilder AddAzureDevOpsClient(string organization, string project, string accessToken)
        {
            Uri baseAddress = UrlHelper.GetBaseAddress(organization, project);

            services.AddTransient(_ => new AuthenticationHandler(accessToken));

            return services
                .AddHttpClient<AzureDevOpsClient>(client => client.BaseAddress = baseAddress)
                .AddHttpMessageHandler<AuthenticationHandler>();
        }

        /// <summary>Registers an Azure DevOps client for one organization and project.</summary>
        /// <param name="organizationUri">Azure DevOps organization URL.</param>
        /// <param name="project">Azure DevOps project name or ID.</param>
        /// <param name="accessToken">Azure DevOps token. This can be System.AccessToken from Azure Pipelines.</param>
        /// <returns>The HTTP client builder.</returns>
        public IHttpClientBuilder AddAzureDevOpsClient(Uri organizationUri, string project, string accessToken)
        {
            Uri baseAddress = UrlHelper.GetBaseAddress(organizationUri, project);

            services.AddTransient(_ => new AuthenticationHandler(accessToken));

            return services
                .AddHttpClient<AzureDevOpsClient>(client => client.BaseAddress = baseAddress)
                .AddHttpMessageHandler<AuthenticationHandler>();
        }
    }
}
