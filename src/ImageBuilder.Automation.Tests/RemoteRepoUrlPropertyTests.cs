// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.ImageBuilder.Automation.Tests;

/// <summary>
/// Property tests for <see cref="RemoteRepo"/> URL construction: for any
/// repository identity and any token, the authenticated clone URL must parse
/// as a valid URI, embed the token losslessly (escaping round-trips), and
/// differ from <see cref="RemoteRepo.CloneUrl"/> only by the credential.
/// </summary>
[TestClass]
public class RemoteRepoUrlPropertyTests
{
    // GitHub/AzDO-ish identifiers. ".."-only names are excluded because a
    // bare ".." path segment is collapsed by URI normalization; the hosting
    // services do not allow such names.
    private static Gen<string> Name =>
        Gen.String[Gen.Char["abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_."], 1, 20]
            .Where(name => name.Trim('.').Length > 0);

    // Printable ASCII including every URI-hostile character, plus some
    // non-ASCII. Tokens are where escaping bugs live.
    private static Gen<string> Token =>
        Gen.String[Gen.Char[" !\"#$%&'()*+,-./0123456789:;<=>?@ABCXYZ[\\]^_`abcxyz{|}~éπ"], 1, 30];

    [TestMethod]
    public void GitHubAuthenticatedCloneUrlEmbedsTokenLosslessly() =>
        Gen.Select(Name, Name, Token)
            .Sample(
                (owner, name, token) =>
                {
                    var repo = new GitHubRepo(owner, name);
                    Uri url = repo.GetAuthenticatedCloneUrl(token);

                    int separator = url.UserInfo.IndexOf(':');
                    separator.ShouldBeGreaterThan(0);
                    url.UserInfo[..separator].ShouldBe("x-access-token");
                    Uri.UnescapeDataString(url.UserInfo[(separator + 1)..]).ShouldBe(token);

                    url.Scheme.ShouldBe(Uri.UriSchemeHttps);
                    url.Host.ShouldBe(repo.CloneUrl.Host);
                    url.AbsolutePath.ShouldBe(repo.CloneUrl.AbsolutePath);
                }
            );

    [TestMethod]
    public void AzdoAuthenticatedCloneUrlEmbedsTokenLosslessly() =>
        Gen.Select(Name, Name, Name, Token)
            .Sample(
                (org, project, name, token) =>
                {
                    var repo = new AzdoRepo(org, project, name);
                    Uri url = repo.GetAuthenticatedCloneUrl(token);

                    int separator = url.UserInfo.IndexOf(':');
                    separator.ShouldBeGreaterThan(0);
                    url.UserInfo[..separator].ShouldBe("azdo");
                    Uri.UnescapeDataString(url.UserInfo[(separator + 1)..]).ShouldBe(token);

                    url.Scheme.ShouldBe(Uri.UriSchemeHttps);
                    url.Host.ShouldBe(repo.CloneUrl.Host);
                    url.AbsolutePath.ShouldBe(repo.CloneUrl.AbsolutePath);
                }
            );

    [TestMethod]
    public void CloneUrlNeverContainsCredentials() =>
        Gen.Select(Name, Name, Name)
            .Sample(
                (owner, project, name) =>
                {
                    new GitHubRepo(owner, name).CloneUrl.UserInfo.ShouldBeEmpty();
                    new AzdoRepo(owner, project, name).CloneUrl.UserInfo.ShouldBeEmpty();
                }
            );

    [TestMethod]
    public void EmptyTokenYieldsBareCloneUrl() =>
        Gen.Select(Name, Name, Name)
            .Sample(
                (owner, project, name) =>
                {
                    var gitHubRepo = new GitHubRepo(owner, name);
                    gitHubRepo.GetAuthenticatedCloneUrl("").ShouldBe(gitHubRepo.CloneUrl);

                    var azdoRepo = new AzdoRepo(owner, project, name);
                    azdoRepo.GetAuthenticatedCloneUrl("").ShouldBe(azdoRepo.CloneUrl);
                }
            );

    [TestMethod]
    public void LocalRepoIgnoresToken() =>
        Token.Sample(token =>
        {
            var repo = new LocalRepo("/tmp/repos/test-repo");
            repo.GetAuthenticatedCloneUrl(token).ShouldBe(repo.CloneUrl);
            repo.CloneUrl.Scheme.ShouldBe(Uri.UriSchemeFile);
        });
}
