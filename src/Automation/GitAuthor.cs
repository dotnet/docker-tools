// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Automation;

/// <summary>
/// The identity used for git commits.
/// </summary>
/// <param name="Name">The committer's name (e.g. "dotnet-docker-bot").</param>
/// <param name="Email">The committer's email address.</param>
public sealed record GitAuthor(string Name, string Email);
