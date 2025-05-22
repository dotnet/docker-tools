// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.ComponentModel.Composition;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder;

public interface IMcrStatusClientFactory
{
    IMcrStatusClient Create(IServiceConnection serviceConnection);
}

[Export(typeof(IMcrStatusClientFactory))]
[method: ImportingConstructor]
public class McrStatusClientFactory(
    IHttpClientProvider httpClientProvider,
    IAzureTokenCredentialProvider tokenCredentialProvider,
    ILoggerService loggerService)
    : IMcrStatusClientFactory
{
    private readonly IHttpClientProvider _httpClientProvider = httpClientProvider;
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly ILoggerService _loggerService = loggerService;

    public IMcrStatusClient Create(IServiceConnection serviceConnection)
    {
        return new McrStatusClient(_httpClientProvider, _loggerService, _tokenCredentialProvider, serviceConnection);
    }
}
