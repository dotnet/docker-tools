// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Octokit;

namespace Microsoft.DotNet.GitAutomation.GitHub;

/// <summary>
/// Provides the credentials and identity used to access a GitHub repository.
/// </summary>
/// <param name="Credentials">Credentials valid for the repository.</param>
/// <param name="Identity">The git identity represented by the token.</param>
/// <param name="Client">An Octokit client authenticated with the token.</param>
public sealed record GitHubAccess(ICredentialStore Credentials, AutomationIdentity Identity, IGitHubClient Client);
