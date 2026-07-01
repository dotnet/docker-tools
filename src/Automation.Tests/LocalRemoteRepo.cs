// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation.Tests;

/// <summary>
/// A <see cref="RemoteRepo"/> that points at a local repository (e.g. a bare
/// repo on disk exposed via a <c>file://</c> URL), so automation operations run
/// fully offline against real git. The token is irrelevant for local repos and
/// is ignored.
/// </summary>
internal sealed record LocalRemoteRepo(Uri Url) : RemoteRepo
{
    public override Uri CloneUrl => Url;

    // protected (not protected internal): the base member is protected internal,
    // but its internal half is not accessible across assemblies, so an external
    // override is declared protected.
    protected override Uri GetAuthenticatedCloneUrl(string token) => Url;
}
