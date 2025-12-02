// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.DotNet.ImageBuilder.Configuration;

public static class ConfigurationExtensions
{
    public static void AddPublishConfiguration(this IHostApplicationBuilder builder)
    {
        var publishConfigSection = builder.Configuration.GetSection(PublishConfiguration.ConfigurationKey);
        builder.Services.AddOptions<PublishConfiguration>().Bind(publishConfigSection);
    }
}
