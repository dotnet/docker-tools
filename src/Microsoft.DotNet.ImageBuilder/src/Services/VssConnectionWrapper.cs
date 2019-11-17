// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    internal class VssConnectionWrapper : IVssConnection
    {
        private readonly VssConnection inner;

        public VssConnectionWrapper(VssConnection inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public void Dispose()
        {
            this.inner.Dispose();
        }

        public IProjectHttpClient GetProjectHttpClient()
        {
            return new ProjectHttpClientWrapper(this.inner.GetClient<ProjectHttpClient>());
        }

        public IBuildHttpClient GetBuildHttpClient()
        {
            return new BuildHttpClientWrapper(this.inner.GetClient<BuildHttpClient>());
        }
    }
}
