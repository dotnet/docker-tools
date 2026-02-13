// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Provides access to host-configured loggers for code paths that cannot be created through DI.
/// Prefer constructor-injected <see cref="ILogger{TCategoryName}"/> in instanced services and commands.
/// Use this factory only from truly static flows.
/// </summary>
internal static class StandaloneLoggerFactory
{
    /// <summary>
    /// Creates a typed logger from the host's service provider.
    /// Intended for static code paths that still need host-configured logging behavior.
    /// </summary>
    public static ILogger<T> CreateLogger<T>() =>
        ImageBuilder.Services.GetRequiredService<ILogger<T>>();

    /// <summary>
    /// Creates a category-based logger from the host's service provider.
    /// Use this when a typed logger cannot be used (for example static utility categories).
    /// </summary>
    public static ILogger CreateLogger(string categoryName) =>
        ImageBuilder.Services.GetRequiredService<ILoggerFactory>().CreateLogger(categoryName);
}
