// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    [Export(typeof(IVssConnectionFactory))]
    internal class VssConnectionFactory : IVssConnectionFactory
    {
        public IVssConnection Create(Uri baseUrl, VssCredentials credentials)
        {
            return new VssConnectionWrapper(new VssConnection(baseUrl, credentials));
        }
    }
}
