// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ImageBuilder;

public interface ICopyImageServiceFactory
{
    ICopyImageService Create(IServiceConnection serviceConnection);
}

public class CopyImageServiceFactory(
    IAzureTokenCredentialProvider tokenCredentialProvider,
    ILoggerService loggerService)
    : ICopyImageServiceFactory
{
    private readonly IAzureTokenCredentialProvider _tokenCredentialProvider = tokenCredentialProvider;
    private readonly ILoggerService _loggerService = loggerService;

    public ICopyImageService Create(IServiceConnection serviceConnection)
    {
        return new CopyImageService(_loggerService, _tokenCredentialProvider, serviceConnection);
    }
}
