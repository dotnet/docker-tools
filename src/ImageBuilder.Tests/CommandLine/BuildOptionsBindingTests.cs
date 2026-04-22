// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.ImageBuilder.Commands;
using Shouldly;
using Xunit;
using static Microsoft.DotNet.ImageBuilder.Tests.CommandLine.OptionsBindingTestHelper;

namespace Microsoft.DotNet.ImageBuilder.Tests.CommandLine;

public class BuildOptionsBindingTests
{
    [Fact]
    public void RegistryCredentials_SingleCredential()
    {
        BuildOptions options = ParseAndBind<BuildOptions>(["--registry-creds", "mcr.microsoft.com=myuser;mypass"]);
        options.CredentialsOptions.Credentials.ShouldContainKey("mcr.microsoft.com");
        options.CredentialsOptions.Credentials["mcr.microsoft.com"].Username.ShouldBe("myuser");
        options.CredentialsOptions.Credentials["mcr.microsoft.com"].Password.ShouldBe("mypass");
    }

    [Fact]
    public void RegistryCredentials_MultipleCredentials()
    {
        string[] args =
        [
            "--registry-creds", "reg1.io=user1;pass1",
            "--registry-creds", "reg2.io=user2;pass2",
        ];

        BuildOptions options = ParseAndBind<BuildOptions>(args);

        options.CredentialsOptions.Credentials.Count.ShouldBe(2);
        options.CredentialsOptions.Credentials["reg1.io"].Username.ShouldBe("user1");
        options.CredentialsOptions.Credentials["reg2.io"].Password.ShouldBe("pass2");
    }

    [Fact]
    public void BuildArgs_ParsesDictionaryValues()
    {
        string[] args =
        [
            "--build-arg", "SDK_VERSION=8.0",
            "--build-arg", "RUNTIME=aspnet",
        ];

        BuildOptions options = ParseAndBind<BuildOptions>(args);

        options.BuildArgs.ShouldContainKeyAndValue("SDK_VERSION", "8.0");
        options.BuildArgs.ShouldContainKeyAndValue("RUNTIME", "aspnet");
    }

    [Fact]
    public void Variables_ParsesDictionaryValues()
    {
        BuildOptions options = ParseAndBind<BuildOptions>(["--var", "branch=main", "--var", "version=8.0"]);
        options.Variables.ShouldContainKeyAndValue("branch", "main");
        options.Variables.ShouldContainKeyAndValue("version", "8.0");
    }

    [Fact]
    public void BooleanFlags_DefaultToFalse()
    {
        BuildOptions options = ParseAndBind<BuildOptions>([]);
        options.IsPushEnabled.ShouldBeFalse();
        options.NoCache.ShouldBeFalse();
        options.IsRetryEnabled.ShouldBeFalse();
        options.IsSkipPullingEnabled.ShouldBeFalse();
        options.IsDryRun.ShouldBeFalse();
    }

    [Fact]
    public void BooleanFlags_SetWhenPresent()
    {
        BuildOptions options = ParseAndBind<BuildOptions>(["--push", "--no-cache", "--retry"]);
        options.IsPushEnabled.ShouldBeTrue();
        options.NoCache.ShouldBeTrue();
        options.IsRetryEnabled.ShouldBeTrue();
    }

    [Fact]
    public void ManifestOption_DefaultsToManifestJson()
    {
        BuildOptions options = ParseAndBind<BuildOptions>([]);
        options.Manifest.ShouldBe("manifest.json");
    }

    [Fact]
    public void ManifestOption_OverriddenWhenSpecified()
    {
        BuildOptions options = ParseAndBind<BuildOptions>(["--manifest", "custom-manifest.json"]);
        options.Manifest.ShouldBe("custom-manifest.json");
    }
}
