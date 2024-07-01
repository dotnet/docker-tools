// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable
namespace Microsoft.DotNet.ImageBuilder
{
    public interface IDotNetReleasesService
    {
        Task<Dictionary<string, DateOnly?>> GetProductEolDatesFromReleasesJson();
    }
}
#nullable disable
