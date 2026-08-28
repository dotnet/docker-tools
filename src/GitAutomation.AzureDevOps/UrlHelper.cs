// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

internal static class UrlHelper
{
    public static Uri GetBaseAddress(string organization, string project)
    {
        organization = Uri.EscapeDataString(organization);
        project = Uri.EscapeDataString(project);
        return new Uri($"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/");
    }

    public static Uri GetBaseAddress(Uri organizationUri, string project)
    {
        string organizationPath = organizationUri.AbsolutePath.TrimEnd('/');
        project = Uri.EscapeDataString(project);

        var baseAddress = new UriBuilder(organizationUri)
        {
            Path = $"{organizationPath}/{project}/_apis/git/repositories/",
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return baseAddress.Uri;
    }
}
