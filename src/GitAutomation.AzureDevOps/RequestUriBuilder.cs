// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.DotNet.GitAutomation.AzureDevOps;

internal sealed class RequestUriBuilder(string repository)
{
    private const string ApiVersion = "7.1";

    private readonly UriBuilder _uri = new()
    {
        Scheme = string.Empty,
        Host = string.Empty,
        Path = Uri.EscapeDataString(repository),
        Query = $"api-version={ApiVersion}",
    };

    public RequestUriBuilder AppendPath(string segment)
    {
        segment = Uri.EscapeDataString(segment);

        _uri.Path += $"/{segment}";

        return this;
    }

    public RequestUriBuilder AppendPath(int segment)
    {
        return AppendPath(segment.ToString(CultureInfo.InvariantCulture));
    }

    public RequestUriBuilder AddQueryParameter(string name, string value)
    {
        name = Uri.EscapeDataString(name);
        value = Uri.EscapeDataString(value);

        _uri.Query += $"&{name}={value}";

        return this;
    }

    public string Build() => _uri.ToString();
}
