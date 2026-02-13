// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Microsoft.DotNet.ImageBuilder.Signing;

/// <summary>
/// Extension methods for registering signing services.
/// </summary>
public static class SigningServiceExtensions
{
    /// <summary>
    /// Adds container image signing services to the service collection.
    /// </summary>
    public static IHostApplicationBuilder AddSigningServices(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IEsrpSigningService, EsrpSigningService>();
        builder.Services.TryAddSingleton<IPayloadSigningService, PayloadSigningService>();
        builder.Services.TryAddSingleton<IBulkImageSigningService, BulkImageSigningService>();
        builder.Services.TryAddSingleton<ISigningRequestGenerator, SigningRequestGenerator>();

        return builder;
    }
}
