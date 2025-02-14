// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Services
{
    public interface IAzdoGitHttpClientFactory
    {
        IAzdoGitHttpClient GetClient(Uri baseUrl, VssCredentials credentials);
    }
}
