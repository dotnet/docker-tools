// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.DotNet.ImageBuilder.Commands;

public class GitHubAuthOptions
{
    public string? AuthToken { get; set; } = null;

    public string? PrivateKeyFilePath { get; set; } = null;
}

public class GitHubAuthOptionsBuilder : OptionsBuilder<GitHubAuthOptionsBuilder>
{
    public GitHubAuthOptionsBuilder WithAuthToken(
        bool isRequired = false,
        string? defaultValue = null,
        string description = "GitHub personal access token (classic or fine-grained)")
    {
        return AddSymbol(
            "auth-token",
            nameof(GitHubAuthOptions.AuthToken),
            isRequired,
            defaultValue,
            description);
    }

    public GitHubAuthOptionsBuilder WithPrivateKeyFilePath(
        bool isRequired = false,
        string? defaultValue = null,
        string description = "Path to the private key file (.pem) for GitHub App authentication")
    {
        return AddSymbol(
            "private-key-file",
            nameof(GitHubAuthOptions.PrivateKeyFilePath),
            isRequired,
            defaultValue,
            description);
    }
}
