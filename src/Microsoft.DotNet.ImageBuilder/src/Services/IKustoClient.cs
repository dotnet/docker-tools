// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.DotNet.ImageBuilder.Commands;

namespace Microsoft.DotNet.ImageBuilder.Services
{
    public interface IKustoClient
    {
        Task IngestFromCsvStreamAsync(Stream csv, IngestKustoImageInfoOptions options);
    }
}
