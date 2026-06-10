// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ImageBuilder.Oras;

/// <inheritdoc/>
public class OrasServiceFactory(
    IRegistryCredentialsProvider credentialsProvider,
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IFileSystem fileSystem,
    ILoggerFactory loggerFactory) : IOrasServiceFactory
{
    private readonly IRegistryCredentialsProvider _credentialsProvider = credentialsProvider;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _cache = cache;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly ILogger<OrasDotNetService> _logger = loggerFactory.CreateLogger<OrasDotNetService>();

    /// <inheritdoc/>
    public IOrasService Create(IRegistryCredentialsHost? credsHost = null) =>
        new OrasDotNetService(
            _credentialsProvider,
            _httpClientFactory,
            _cache,
            _fileSystem,
            _logger,
            credsHost);
}
