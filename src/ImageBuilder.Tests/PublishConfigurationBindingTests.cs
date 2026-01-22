// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.ImageBuilder.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Microsoft.DotNet.ImageBuilder.Tests;

/// <summary>
/// Tests that <see cref="PublishConfiguration"/> binds correctly from JSON
/// via <see cref="Microsoft.Extensions.Configuration"/>.
/// </summary>
public class PublishConfigurationBindingTests
{
    private const string FullConfigJson = """
        {
          "PublishConfiguration": {
            "BuildRegistry": {
              "Server": "build.azurecr.io"
            },
            "PublishRegistry": {
              "Server": "publish.azurecr.io"
            },
            "InternalMirrorRegistry": {
              "Server": "internal-mirror.azurecr.io"
            },
            "PublicMirrorRegistry": {
              "Server": "public-mirror.azurecr.io"
            },
            "RegistryAuthentication": {
              "build.azurecr.io": {
                "ServiceConnection": {
                  "Name": "build-sc",
                  "TenantId": "tenant-build",
                  "ClientId": "client-build",
                  "Id": "id-build"
                },
                "ResourceGroup": "rg-build",
                "Subscription": "sub-build"
              },
              "publish.azurecr.io": {
                "ServiceConnection": {
                  "Name": "publish-sc",
                  "TenantId": "tenant-publish",
                  "ClientId": "client-publish",
                  "Id": "id-publish"
                },
                "ResourceGroup": "rg-publish",
                "Subscription": "sub-publish"
              }
            }
          }
        }
        """;

    [Fact]
    public void AddPublishConfiguration_BindsAllRegistryEndpoints()
    {
        PublishConfiguration config = BuildConfiguration(FullConfigJson);

        config.BuildRegistry.ShouldNotBeNull();
        config.BuildRegistry.Server.ShouldBe("build.azurecr.io");

        config.PublishRegistry.ShouldNotBeNull();
        config.PublishRegistry.Server.ShouldBe("publish.azurecr.io");

        config.InternalMirrorRegistry.ShouldNotBeNull();
        config.InternalMirrorRegistry.Server.ShouldBe("internal-mirror.azurecr.io");

        config.PublicMirrorRegistry.ShouldNotBeNull();
        config.PublicMirrorRegistry.Server.ShouldBe("public-mirror.azurecr.io");
    }

    [Fact]
    public void AddPublishConfiguration_BindsRegistryAuthenticationDictionary()
    {
        PublishConfiguration config = BuildConfiguration(FullConfigJson);

        config.RegistryAuthentication.Count.ShouldBe(2);
        config.RegistryAuthentication.ShouldContainKey("build.azurecr.io");
        config.RegistryAuthentication.ShouldContainKey("publish.azurecr.io");
    }

    [Fact]
    public void AddPublishConfiguration_BindsNestedServiceConnection()
    {
        PublishConfiguration config = BuildConfiguration(FullConfigJson);

        var buildAuth = config.RegistryAuthentication["build.azurecr.io"];
        buildAuth.ServiceConnection.ShouldNotBeNull();
        buildAuth.ServiceConnection.Name.ShouldBe("build-sc");
        buildAuth.ServiceConnection.TenantId.ShouldBe("tenant-build");
        buildAuth.ServiceConnection.ClientId.ShouldBe("client-build");
        buildAuth.ServiceConnection.Id.ShouldBe("id-build");

        buildAuth.ResourceGroup.ShouldBe("rg-build");
        buildAuth.Subscription.ShouldBe("sub-build");
    }

    [Fact]
    public void AddPublishConfiguration_GetKnownRegistries_ReturnsAllEndpoints()
    {
        PublishConfiguration config = BuildConfiguration(FullConfigJson);
        var knownRegistries = config.GetKnownRegistries().ToList();

        knownRegistries.Count.ShouldBe(4);
        knownRegistries.ShouldContain(r => r.Server == "build.azurecr.io");
        knownRegistries.ShouldContain(r => r.Server == "publish.azurecr.io");
        knownRegistries.ShouldContain(r => r.Server == "internal-mirror.azurecr.io");
        knownRegistries.ShouldContain(r => r.Server == "public-mirror.azurecr.io");
    }

    [Fact]
    public void FindRegistryAuthentication_ExactMatch_ReturnsAuth()
    {
        PublishConfiguration config = BuildConfiguration(FullConfigJson);

        var auth = config.FindRegistryAuthentication("build.azurecr.io");

        auth.ShouldNotBeNull();
        auth.ServiceConnection?.TenantId.ShouldBe("tenant-build");
    }

    [Fact]
    public void FindRegistryAuthentication_NormalizedAcrName_ReturnsAuth()
    {
        PublishConfiguration config = BuildConfiguration(FullConfigJson);

        var auth = config.FindRegistryAuthentication("build");

        auth.ShouldNotBeNull();
        auth.ServiceConnection?.TenantId.ShouldBe("tenant-build");
    }

    [Fact]
    public void FindRegistryAuthentication_NotFound_ReturnsNull()
    {
        PublishConfiguration config = BuildConfiguration(FullConfigJson);

        var auth = config.FindRegistryAuthentication("nonexistent.azurecr.io");

        auth.ShouldBeNull();
    }

    [Fact]
    public void AddPublishConfiguration_PartialConfig_HandlesNulls()
    {
        const string partialJson = """
            {
              "PublishConfiguration": {
                "BuildRegistry": {
                  "Server": "build.azurecr.io"
                }
              }
            }
            """;

        PublishConfiguration config = BuildConfiguration(partialJson);

        config.BuildRegistry.ShouldNotBeNull();
        config.BuildRegistry.Server.ShouldBe("build.azurecr.io");
        config.PublishRegistry.ShouldBeNull();
        config.InternalMirrorRegistry.ShouldBeNull();
        config.PublicMirrorRegistry.ShouldBeNull();
        config.RegistryAuthentication.ShouldBeEmpty();
    }

    [Fact]
    public void AddPublishConfiguration_EmptyConfig_DefaultsToEmpty()
    {
        const string emptyJson = """
            {
              "PublishConfiguration": { }
            }
            """;

        PublishConfiguration config = BuildConfiguration(emptyJson);

        config.BuildRegistry.ShouldBeNull();
        config.PublishRegistry.ShouldBeNull();
        config.InternalMirrorRegistry.ShouldBeNull();
        config.PublicMirrorRegistry.ShouldBeNull();
        config.RegistryAuthentication.ShouldBeEmpty();
    }

    [Fact]
    public void AddPublishConfiguration_SharedAuthentication_MultipleRegistriesCanUseSameAuth()
    {
        const string sharedAuthJson = """
            {
              "PublishConfiguration": {
                "BuildRegistry": {
                  "Server": "shared.azurecr.io"
                },
                "InternalMirrorRegistry": {
                  "Server": "shared.azurecr.io"
                },
                "RegistryAuthentication": {
                  "shared.azurecr.io": {
                    "ServiceConnection": {
                      "TenantId": "shared-tenant",
                      "ClientId": "shared-client"
                    }
                  }
                }
              }
            }
            """;

        PublishConfiguration config = BuildConfiguration(sharedAuthJson);

        var buildAuth = config.FindRegistryAuthentication(config.BuildRegistry!.Server!);
        var mirrorAuth = config.FindRegistryAuthentication(config.InternalMirrorRegistry!.Server!);

        buildAuth.ShouldNotBeNull();
        mirrorAuth.ShouldNotBeNull();
        buildAuth.ShouldBeSameAs(mirrorAuth);
        buildAuth.ServiceConnection?.TenantId.ShouldBe("shared-tenant");
    }

    /// <summary>
    /// Helper method that builds a <see cref="PublishConfiguration"/> from JSON
    /// using the actual <see cref="ConfigurationExtensions.AddPublishConfiguration"/> method.
    /// </summary>
    private static PublishConfiguration BuildConfiguration(string json)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        builder.AddPublishConfiguration();

        using var host = builder.Build();
        var options = host.Services.GetRequiredService<IOptions<PublishConfiguration>>();
        return options.Value;
    }
}
