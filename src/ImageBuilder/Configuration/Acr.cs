// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

/// <summary>
/// Strongly-typed reference to an Azure Container Registry (ACR).
/// </summary>
public sealed record Acr
{
    private const string AcrDomain = ".azurecr.io";
    private const string Https = "https://";

    private Acr(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Parses a reference to an <see cref="Acr"/> from a string.
    /// </summary>
    /// <param name="registry">The name, login server, or URL of the ACR.</param>
    public static Acr Parse(string name)
    {
        name = name
            .ToLowerInvariant()
            .TrimStartString(Https)
            .TrimEndString(AcrDomain);

        return new Acr(name);
    }

    /// <summary>
    /// Name of the ACR without the ".azurecr.io" suffix.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// E.g. "myregistry.azurecr.io"
    /// </summary>
    /// <remarks>
    /// This is also sometimes called the "login server".
    /// </remarks>
    public string Server => $"{Name}{AcrDomain}";

    /// <summary>
    /// E.g. "https://myregistry.azurecr.io"
    /// </summary>
    public string RegistryUrl => $"https://{Server}";

    /// <summary>
    /// E.g. "https://myregistry.azurecr.io" as an instance of a <see cref="Uri"/>
    /// </summary>
    public Uri RegistryUri => new(RegistryUrl);
}
