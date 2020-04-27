// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ImageBuilder
{
    [Export(typeof(IAcrClientFactory))]
    public class AcrClientFactory : IAcrClientFactory
    {
        public Task<IAcrClient> CreateAsync(string acrName, string tenant, string username, string password)
        {
            return AcrClient.CreateAsync(acrName, tenant, username, password);
        }
    }
}
