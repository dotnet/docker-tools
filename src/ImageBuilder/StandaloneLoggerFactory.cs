// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.ImageBuilder;

/// <summary>
/// Provides access to host-configured loggers for code paths that cannot be created through DI.
/// Prefer constructor-injected <see cref="ILogger{TCategoryName}"/> in instanced services and commands.
/// Use this factory only from truly static flows.
/// </summary>
internal static class StandaloneLoggerFactory
{
    /// <summary>
    /// The logger factory used to create loggers for static code paths.
    /// The composition root (<c>Program</c>) sets this to the host's logger factory once the host is built.
    /// Defaults to <see cref="NullLoggerFactory.Instance"/> so static flows (for example tests that never
    /// boot the host) can run without logging configured.
    /// </summary>
    public static ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

    /// <summary>
    /// Creates a typed logger from the configured <see cref="LoggerFactory"/>.
    /// Intended for static code paths that still need host-configured logging behavior.
    /// </summary>
    public static ILogger<T> CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

    /// <summary>
    /// Creates a category-based logger from the configured <see cref="LoggerFactory"/>.
    /// Use this when a typed logger cannot be used (for example static utility categories).
    /// </summary>
    public static ILogger CreateLogger(string categoryName) => LoggerFactory.CreateLogger(categoryName);
}
