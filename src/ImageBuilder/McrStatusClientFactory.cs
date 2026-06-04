// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Net.Http;

namespace Microsoft.DotNet.ImageBuilder;

public interface IMcrStatusClientFactory
{
    IMcrStatusClient Create(IServiceConnection serviceConnection);
}

public class McrStatusClientFactory(
    IHttpClientFactory httpClientFactory,
    IAzureTokenCredentialProvider tokenCredentialProvider,
    ILogger<McrStatusClient> logger)
    : IMcrStatusClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly ILogger<McrStatusClient> _logger = logger;

    public IMcrStatusClient Create(IServiceConnection serviceConnection)
    {
        return new McrStatusClient(_httpClientFactory, _logger, _tokenCredentialProvider, serviceConnection);
    }
}
