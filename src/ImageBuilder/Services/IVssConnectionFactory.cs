// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.Services.Common;

namespace Microsoft.DotNet.DockerTools.ImageBuilder.Services
{
    public interface IVssConnectionFactory
    {
        IVssConnection Create(Uri baseUrl, VssCredentials credentials);
    }
}
