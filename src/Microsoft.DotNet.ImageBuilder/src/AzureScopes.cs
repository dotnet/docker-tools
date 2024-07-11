// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

#nullable enable
internal static class AzureScopes
{
    public const string ScopeSuffix = "/.default";
    public const string DefaultAzureManagementScope = "https://management.azure.com" + ScopeSuffix;
    public const string ContainerRegistryScope = "https://containerregistry.azure.net" + ScopeSuffix;
    public const string McrStatusScope = "api://c00053c3-a979-4ee6-b94e-941881e62d8e" + ScopeSuffix;
    public const string LogAnalyticsScope = "https://api.loganalytics.io" + ScopeSuffix;
}
