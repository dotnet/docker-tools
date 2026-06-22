// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// Identifies a git repository hosted on a remote service.
/// </summary>
public abstract record RemoteRepo
{
    /// <summary>
    /// The HTTPS URL used to clone the repository, without any credentials.
    /// </summary>
    public abstract Uri CloneUrl { get; }

    /// <summary>
    /// The HTTPS URL used to clone the repository, with the given token
    /// embedded as a credential. Never log this URL.
    /// </summary>
    internal abstract Uri GetAuthenticatedCloneUrl(string token);
}
