// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


namespace Microsoft.DotNet.ImageBuilder;

public interface IMcrStatusClientFactory
{
    IMcrStatusClient Create(IServiceConnection serviceConnection);
}

public class McrStatusClientFactory(
    IHttpClientProvider httpClientProvider,
    IAzureTokenCredentialProvider tokenCredentialProvider,
    ILogger<McrStatusClient> logger)
    : IMcrStatusClientFactory
{
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly ILogger<McrStatusClient> _logger = logger;

    public IMcrStatusClient Create(IServiceConnection serviceConnection)
    {
        return new McrStatusClient(_httpClientProvider, _logger, _tokenCredentialProvider, serviceConnection);
    }
}
